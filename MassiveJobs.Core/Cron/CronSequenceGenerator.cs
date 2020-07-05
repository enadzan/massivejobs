using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MassiveJobs.Core.Cron
{
    /// <summary>
    /// Modeled by Java Spring Frameworks's CronSequenceGenerator
	/// https://github.com/spring-projects/spring-framework/blob/master/spring-context/src/main/java/org/springframework/scheduling/support/CronSequenceGenerator.java
    /// </summary>
    class CronSequenceGenerator
	{
		public string Expression { get; }

        private readonly TimeZoneInfo _timeZoneInfo;
        private readonly BitArray _months = new BitArray(13); // zero index is not used, that's why 13 and not 12
		private readonly BitArray _daysOfMonth = new BitArray(32); // zero index is not used
		private readonly BitArray _daysOfWeek = new BitArray(8); // seven can be sunday
		private readonly BitArray _hours = new BitArray(24);
		private readonly BitArray _minutes = new BitArray(60);
		private readonly BitArray _seconds = new BitArray(60);

		private bool _isIntervalBased;

		public CronSequenceGenerator(string expression, string timeZoneInfoId = null)
			: this(expression, string.IsNullOrWhiteSpace(timeZoneInfoId) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId))
		{
		}

        public CronSequenceGenerator(string expression, TimeZoneInfo timeZoneInfo)
        {
			Expression = expression;
			_timeZoneInfo = timeZoneInfo;

			Parse();
        }

		public DateTime NextUtc(DateTime dateTimeUtc)
		{
			var truncatedUtc = dateTimeUtc.TruncateMs();

			var dateTime = TimeZoneInfo.ConvertTimeFromUtc(truncatedUtc, _timeZoneInfo);

			var offsets = new List<TimeSpan>();

			if (_timeZoneInfo.IsAmbiguousTime(dateTime))
			{
				offsets.AddRange(_timeZoneInfo.GetAmbiguousTimeOffsets(dateTime));
			}
			else
			{
				offsets.Add(_timeZoneInfo.GetUtcOffset(dateTime));
			}

			var possibleResults = new List<DateTime?>();

			DateTime nextDateTime, originalDateTime;
			foreach (var offset in offsets) 
			{
				nextDateTime = originalDateTime = DateTime.SpecifyKind(truncatedUtc.Add(offset), DateTimeKind.Unspecified);
				
				DoNext(ref nextDateTime, nextDateTime.Year);

				if (nextDateTime == originalDateTime)
				{
					// We arrived at the original timestamp - round up to the next whole second and try again...
					nextDateTime = nextDateTime.AddSeconds(1);
					DoNext(ref nextDateTime, nextDateTime.Year);
				}

				// try to skip invalid time for the time zone
				while (_timeZoneInfo.IsInvalidTime(nextDateTime))
				{
					nextDateTime = nextDateTime.AddSeconds(1);
					DoNext(ref nextDateTime, nextDateTime.Year);
				}

				if (_timeZoneInfo.IsAmbiguousTime(nextDateTime))
				{
					var nextOffsets = _timeZoneInfo.GetAmbiguousTimeOffsets(nextDateTime);

					if (_isIntervalBased)
					{
						foreach (var nextOffset in nextOffsets)
						{
							possibleResults.Add(DateTime.SpecifyKind(nextDateTime.Subtract(nextOffset), DateTimeKind.Utc));
						}
					}
					else
                    {
						// if the expression is not interval based (doesn't have */- in sec, min, hour field)
						// we only use one of the intervals (the last one), which has the largest offset and yields smallest time after subtract.
						possibleResults.Add(DateTime.SpecifyKind(nextDateTime.Subtract(nextOffsets.Last()), DateTimeKind.Utc));
                    }
				}
				else
				{
					possibleResults.Add(TimeZoneInfo.ConvertTimeToUtc(nextDateTime));
				}
			}

			return possibleResults
				.OrderBy(utc => utc)
				.FirstOrDefault(utc => utc > dateTimeUtc)
				?? throw new Exception("Possible bug in code - couldn't find next cron time");
		}

		/// <summary>
		/// Determine whether the specified expression represents a valid cron pattern.
		/// </summary>
		/// <param name="expression">the expression to evaluate</param>
		/// <returns>true if the given expression is a valid cron expression, otherwise false</returns>
		public static bool IsValidExpression(string expression) 
		{
			if (expression == null) 
			{
				return false;
			}

            try
            {
				new CronSequenceGenerator(expression);
				return true;
			}
			catch (ArgumentException)
            {
				return false;
			}
		}

		private void DoNext(ref DateTime dateTime, int dot)
		{
			var resets = new List<FieldType>();

			var second = dateTime.Second;
			var updateSecond = FindNext(ref dateTime, _seconds, second, FieldType.Seconds, FieldType.Minutes, new List<FieldType>());
			if (second == updateSecond)
			{
				resets.Add(FieldType.Seconds);
			}

			var minute = dateTime.Minute;
			var updateMinute = FindNext(ref dateTime, _minutes, minute, FieldType.Minutes, FieldType.Hours, resets);
			if (minute == updateMinute)
			{
				resets.Add(FieldType.Minutes);
			}
			else
			{
				DoNext(ref dateTime, dot);
			}

			var hour = dateTime.Hour;
			var updateHour = FindNext(ref dateTime, _hours, hour, FieldType.Hours, FieldType.DaysOfWeek, resets);
			if (hour == updateHour)
			{
				resets.Add(FieldType.Hours);
			}
			else
			{
				DoNext(ref dateTime, dot);
			}

			var dayOfWeek = (int)dateTime.DayOfWeek;
			var dayOfMonth = dateTime.Day;
			var updateDayOfMonth = FindNextDay(ref dateTime, _daysOfMonth, dayOfMonth, _daysOfWeek, dayOfWeek, resets);
			if (dayOfMonth == updateDayOfMonth)
			{
				resets.Add(FieldType.DaysOfMonth);
			}
			else
			{
				DoNext(ref dateTime, dot);
			}

			var month = dateTime.Month;
			var updateMonth = FindNext(ref dateTime, _months, month, FieldType.Months, FieldType.Years, resets);
			if (month != updateMonth)
			{
				if (dateTime.Year - dot > 4)
				{
					throw new ArgumentException($"Invalid cron expression '{Expression}' led to runaway search for next trigger");
				}

				DoNext(ref dateTime, dot);
			}
		}

		private int FindNextDay(ref DateTime dateTime, BitArray daysOfMonth, int dayOfMonth, BitArray daysOfWeek, int dayOfWeek, IReadOnlyList<FieldType> resets)
		{
			int count = 0;
			int max = 366;

			while ((!daysOfMonth.Get(dayOfMonth) || !daysOfWeek.Get(dayOfWeek)) && count++ < max)
			{
				dateTime = dateTime.Add(FieldType.DaysOfMonth, 1);
				
				dayOfMonth = dateTime.Day;
				dayOfWeek = (int)dateTime.DayOfWeek;

				dateTime = dateTime.Reset(resets);
			}

			if (count >= max) throw new ArgumentException($"Overflow in day for expression '{Expression}'"); 

			return dayOfMonth;
		}

		/// <summary>
		/// Search the bits provided for the next set bit after the value provided, and reset the dateTime.
		/// </summary>
		/// <param name="dateTime">Current date/time being modified</param>
		/// <param name="bits">a <see cref="BitArray"/> representing the allowed values of the field</param>
		/// <param name="value">the current value of the field</param>
		/// <param name="field">the field to increment in the dateTime. See <see cref="FieldType"/> for the valid field values).</param>
		/// <param name="nextField">next field to be incremented if roll-over happens (<see cref="FieldType"/>)</param>
		/// <param name="lowerOrders">fields that should be reset (i.e. the ones of lower significance than the field of interest)</param>
		/// <returns>The value of the field that is next in the sequence</returns>
		private int FindNext(ref DateTime dateTime, BitArray bits, int value, FieldType field, FieldType nextField, IReadOnlyList<FieldType> lowerOrders)
		{
			int nextValue = bits.GetNextSetBit(value);
			
			// roll over if needed
			if (nextValue == -1)
			{
				dateTime = dateTime
					.Add(nextField, 1)
					.Reset(field);

				nextValue = bits.GetNextSetBit(0);
			}

			if (nextValue != value)
			{
				dateTime = dateTime
					.SetField(field, nextValue)
					.Reset(lowerOrders);
			}

			return nextValue;
		}

        private void Parse()
		{
			var fields = Expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (!AreValidCronFields(fields))
			{
				throw new ArgumentException($"Cron expression must consist of 6 fields (found {fields.Length} in '{Expression}')");
			}

			SetNumberHits(_seconds, fields[0], 0, 60, out var isIntervalSec);
			SetNumberHits(_minutes, fields[1], 0, 60, out var isIntervalMin);
			SetNumberHits(_hours, fields[2], 0, 24, out var isIntervalHr);
			SetDaysOfMonth(_daysOfMonth, fields[3]);
			SetMonths(_months, fields[4]);
			SetDays(_daysOfWeek, ReplaceOrdinals(fields[5], "SUN,MON,TUE,WED,THU,FRI,SAT"), 8);

			if (_daysOfWeek.Get(7)) {
				// Sunday can be represented as 0 or 7
				_daysOfWeek.Set(0, true);
				_daysOfWeek.Set(7, false);
			}

			_isIntervalBased = isIntervalSec || isIntervalMin || isIntervalHr;
		}

		private void SetDaysOfMonth(BitArray bits, string field)
		{
			int max = 31;

			// Days of month start with 1 (in Cron and DateTime) so add one
			SetDays(bits, field, max + 1);

			// ... and remove it from the front
			bits.Set(0, false);
		}

		private void SetDays(BitArray bits, string field, int max)
		{
			if (field.Contains("?"))
			{
				field = "*";
			}

			SetNumberHits(bits, field, 0, max, out _);
		}

		private void SetMonths(BitArray bits, string value)
		{
			int max = 12;
			value = ReplaceOrdinals(value, "FOO,JAN,FEB,MAR,APR,MAY,JUN,JUL,AUG,SEP,OCT,NOV,DEC");

			// Months start with 1 (in Cron and DateTime) so add one
			SetNumberHits(bits, value, 1, max + 1, out _);

            // ... and remove it from the front
			bits.Set(0, false);
		}

		private void SetNumberHits(BitArray bits, string value, int min, int max, out bool isInterval)
		{
			isInterval = false;

			var fields = value.Split(',');

			foreach (var field in fields)
			{
				if (!field.Contains("/"))
				{
					// Not an incrementer so it must be a range (possibly empty)
					var range = GetRange(field, min, max);
					for (var i = range[0]; i <= range[1]; i++)
					{
						bits.Set(i, true);
					}

					isInterval = range[0] != range[1];
				}
				else
				{
					isInterval = true;

					var split = field.Split('/');
					if (split.Length != 2) throw new ArgumentException($"Incrementer has more than two fields: '{field}' in expression '{Expression}'");

					var range = GetRange(split[0], min, max);
					if (!split[0].Contains("-"))
					{
						range[1] = max - 1;
					}

					if (!int.TryParse(split[1], out var delta)) throw new ArgumentException($"Invalid incrementer delta in field '{field}' in expression '{Expression}'");
					if (delta <= 0) throw new ArgumentException($"Incrementer delta must be 1 or higher: '{field}' in expression '{Expression}'");

					for (int i = range[0]; i <= range[1]; i += delta)
					{
						bits.Set(i, true);
					}
				}
			}
		}

		private int[] GetRange(string field, int min, int max)
		{
			int[] result = new int[2];

			if (field.Contains("*"))
			{
				result[0] = min;
				result[1] = max - 1;
				return result;
			}

			if (!field.Contains("-"))
			{
				if (!int.TryParse(field, out var fieldValue)) throw new ArgumentException($"Invalid cron expression field '{field}'");
				result[0] = result[1] = fieldValue;
			}
			else
			{
				var split = field.Split('-');
				if (split.Length != 2) throw new ArgumentException($"Range '{field}' doesn't have two fields in expression '{Expression}'");
				if (!int.TryParse(split[0], out var fromValue)) throw new ArgumentException($"Invalid from value '{split[0]}' in range '{field}'");
				if (!int.TryParse(split[1], out var toValue)) throw new ArgumentException($"Invalid to value '{split[1]}' in range '{field}'");

				result[0] = fromValue;
				result[1] = toValue;
			}

			if (result[0] >= max || result[1] >= max) throw new ArgumentException($"Range exceeds maximum ({max}): '{field}' in expression '{Expression}'");
			if (result[0] < min || result[1] < min) throw new ArgumentException($"Range less than minimum ({min}): '{field}' in expression '{Expression}'");
			if (result[0] > result[1]) throw new ArgumentException($"Invalid inverted range: '{field}' in expression '{Expression}'");

			return result;
		}

		/// <summary>
		/// Replace the values in the comma-separated list (case insensitive) with their index in the list.
		/// </summary>
		/// <returns>a new String with the values from the list replaced</returns>
		private string ReplaceOrdinals(string value, string commaSeparatedList)
		{
			var list = commaSeparatedList.Split(',');
			for (int i = 0; i < list.Length; i++)
			{
				var item = list[i].ToUpper();
				value = value.ToUpper().Replace(item, "" + i);
			}
			return value;
		}

		private static bool AreValidCronFields(IReadOnlyList<string> fields) 
		{
			return fields != null && fields.Count == 6;
		}
    }
}

using System;
using System.Collections.Generic;

namespace MassiveJobs.Core.Cron
{
    internal static class DateTimeExtensions
    {
        public static DateTime Add(this DateTime dateTime, FieldType field, int value)
        {
			var result = dateTime;

            switch (field)
            {
                case FieldType.Seconds:
                    result = result.AddSeconds(value);
                    break;
                case FieldType.Minutes:
                    result = result.AddMinutes(value);
                    break;
                case FieldType.Hours:
                    result = result.AddHours(value);
                    break;
                case FieldType.DaysOfWeek:
                case FieldType.DaysOfMonth:
                    result = result.AddDays(value);
                    break;
                case FieldType.Months:
                    result = result.AddMonths(value);
                    break;
                case FieldType.Years:
                    result = result.AddYears(value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field));
            }

            return result;
        }

        public static DateTime SetField(this DateTime dateTime, FieldType field, int value)
        {
			var result = dateTime;

            switch (field)
            {
                case FieldType.Seconds:
                    result = result.AddSeconds((-result.Second) + value);
                    break;
                case FieldType.Minutes:
                    result = result.AddMinutes((-result.Minute) + value);
                    break;
                case FieldType.Hours:
                    result = result.AddHours((-result.Hour) + value);
                    break;
                case FieldType.DaysOfWeek:
                    result = result.AddDays((-(int)result.DayOfWeek) + value);
                    break;
                case FieldType.DaysOfMonth:
                    result = result.AddDays((-result.Day) + value);
                    break;
                case FieldType.Months:
                    result = result.AddMonths((-result.Month) + value);
                    break;
                case FieldType.Years:
                    result = result.AddYears((-result.Year) + value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field));
            }

            return result;
        }

        public static DateTime Reset(this DateTime dateTime, FieldType field) 
		{
            return SetField(dateTime, field, field == FieldType.DaysOfMonth || field == FieldType.Months ? 1 : 0);
        }

        public static DateTime Reset(this DateTime dateTime, IReadOnlyList<FieldType> fieldTypes) 
		{
			var result = dateTime;

            foreach (var field in fieldTypes)
            {
                result = Reset(result, field);
            }

            return result;
        }

        public static DateTime TruncateMs(this DateTime dateTime)
        {
            return dateTime.AddMilliseconds(-dateTime.Millisecond);
        }
    }
}

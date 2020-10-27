# MassiveJobs.RabbitMqBroker
Open source library for publishing scheduled and "out-of-band" jobs using RabbitMQ message broker. Published jobs can be performed by multiple workers distributed across multiple machines.

## Requirements

- RabbitMQ 3.8+
- .NET Core 2.0+ or .NET Framework 4.6.1+

## Quick Start
### 1. Start RabbitMQ

If you don't have an existing installation of RabbitMQ, the simplest way is to start it in a container. 
The following command will start RabbitMq in a container __that will be immediately removed when stopped__.

```powershell
docker run --rm --hostname rabbit-test --name rabbit-test -d -p 15672:15672 -p 5672:5672 rabbitmq:management
```

Now, you should be able to access RabbitMQ management UI in your browser on: http://localhost:15672 address. 
You can sign in with username __guest__ and password __guest__, if you want to monitor the connections, queues, etc.

### 2. Create a .NET Console Application

We will use .NET Core 3.1 CLI for this quick start, but you can also do it in Visual Studio, with .NET Core or with .NET Framework 4.6.1 or later.
  
Create a folder for the project.

```powershell
mkdir MassiveJobs.QuickStart
cd MassiveJobs.QuickStart
```

Create a new console application project.

```powershell
dotnet new console
```

Test the scaffolded project.

```powershell
dotnet run
```

You should see `Hello World!` after a couple of seconds.

### 3. Add MassiveJobs.RabbitMqBroker to the project

Add a package reference to the `MassiveJobs.RabbitMqBroker`.

```powershell
dotnet add package MassiveJobs.RabbitMqBroker
```

### 4. Edit Program.cs

Use your favorite editor to open Program.cs and enter this code. 
Comments in the code should be enough to give you a basic idea of what is going on.
```csharp
using System;
using MassiveJobs.Core;
using MassiveJobs.RabbitMqBroker;

namespace MassiveJobs.QuickStart
{
    /// <summary>
    /// This is a "job" class. 
    /// It will be instantiated every time a message is received and Perform will be called.
    /// It inherits from the Job generic class. The firt type parameter (MessageReceiver) specifies the type of job,
    /// and the second type parameter (string) specifies the type of parameter expected by the Perform method.
    /// </summary>
    public class MessageReceiver: Job<MessageReceiver, string>
    {
        public override void Perform(string message)
        {
            Console.WriteLine("Job performed: " + message);
        }
    }

    class Program
    {
        private static void Main()
        {
            Console.WriteLine("1: Worker");
            Console.WriteLine("2: Publisher");
            Console.Write("Choose 1 or 2 -> ");

            var startWorkers = Console.ReadLine() != "2";

            // We are not starting job workers if '2' is selected.
            // This is not mandatory, an application can run job workers
            // and publish jobs using the same MassiveJobs instance.

            RabbitMqJobs.Initialize(startWorkers);

            if (startWorkers)
            {
                RunWorker();
            }
            else
            {
                RunPublisher();
            }
        }

        private static void RunWorker()
        {
            Console.WriteLine("Initialized job worker.");
            Console.WriteLine("Press Enter to end the application.");

            Console.ReadLine();
        }

        private static void RunPublisher()
        {
            Console.WriteLine("Initialized job publisher");
            Console.WriteLine("Write the job name and press Enter to publish it (empty job name to end).");

            while (true)
            {
                Console.Write("> ");
                var message = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(message)) break;

                // notice that Publish is a static method on our MessageReceiver class
                // it is available because MessageReceiver inherits from Job<TJob, TArgs>
                MessageReceiver.Publish(message);
            }
        }
    }
}
```

### 5. Test the application

Start three different command prompts (or power shells). Two will be used as workers, and one will be used as publisher.
  
To start a worker go to the project folder and run:
```powershell
dotnet run
```
To start a publisher, go to the project folder and run:
```powershell
dotnet run publisher
```
As you enter messages in the publisher console, you will notice them being processed in one or the other worker, 
but not both. This is because jobs are distributed between the workers.
  
Note that you can start multiple publishers too.
  
Workers and publishers can be on different machines, as long as they can access the RabbitMQ server.

## Distributing Workers Across Multiple Machines
  
To distribute the workers across several machines you will have to configure the information about the RabbitMQ server. At minimum, that means username, password, host name (or ip address), and the port number (if your RabbitMQ server is configured to listen for connections on a non-standard port). In the example above we did not configure any of it because the defaults were sufficient - username: `guest`, password: `guest`, hostname: `localhost`, port: `-1` (= use the default port). 
  
For example, if your RabbitMQ server is running on a machine with the hostname `rabbit.example.local`, listening on the standard port number, and you have created a user `massive` in the RabbitMQ with the password: `d0ntUseTh!s` then you would initialize `RabbitMqJobs` like this.

```csharp
var settings = new RabbitMqSettings
{
    HostNames = new[] { "rabbit.example.com" },
    Username = "massive",
    Password = "d0ntUseTh!s"
};

RabbitMqJobs.Initialize(rabbitMqSettings: settings);
```
  
Or, if you don't want to start the worker threads (ie. to use the process only for publishing jobs), just change the last line to:

```csharp
RabbitMqJobs.Initialize(startWorkers: false, rabbitMqSettings: settings);
```

Now you can deploy workers (and publishers) on multiple machines and run them. If the network connectivity is working (firewalls open etc.) everything should work. Jobs would be routed to workers in a round-robin fashion. Keep in mind that, by default, every MassiveJobs application is starting two worker threads. That means, if you have 3 machines, each running one MassiveJobs application, then the distribution of jobs would look something like this:

* job1 -> machine 1, worker thread 1
* job2 -> machine 1, worker thread 2
* job3 -> machine 2, worker thread 1
* job4 -> machine 2, worker thread 2
* etc.

You might have noticed, in the quick-start example, when we had running two MassiveJobs applications in two posershell windows, two of the messages would go to one window, the next two to the other window and so on. Now you know the reason. 

## Configure Logging
  
__Skip this section if your application is running in a .NET Core hosted environment (ASP.NET Core Web Application or Worker Service).__

It is very important to configure logging in your application running MassiveJobs because that is the only way to see MassiveJobs run-time errors in your application. It is as simple as installing a suitable package and setting the `JobLoggerFactory` on initialization, if your are using one of the following logger libraries:

* log4net (use package `MassiveJobs.Logging.Log4Net`)
* NLog (use package `MassiveJobs.Logging.NLog`)
* Serilog (use package `MassiveJobs.Logging.Serilog`)

For example, if you want to add log4net logging to the quick-start example, first install the `MassiveJobs.Logging.Log4Net` package in your project. After that, initialize log4net library, and finally MassiveJobs.

```csharp
private static void Main()
{
    InitializeLogging();
    
    Console.WriteLine("1: Worker");
    Console.WriteLine("2: Publisher");
    Console.Write("Choose 1 or 2 -> ");

    var startWorkers = Console.ReadLine() != "2";

    // We are not starting job workers if '2' is selected.
    // This is not mandatory, an application can run job workers
    // and publish jobs using the same MassiveJobs instance.

    RabbitMqJobs.Initialize(startWorkers, configureAction: options =>
    {
        options.JobLoggerFactory = new MassiveJobs.Logging.Log4Net.LoggerWrapperFactory();
        // for NLog: options.JobLoggerFactory = new MassiveJobs.Logging.NLog.LoggerWrapperFactory(); 
        // for Serilog: options.JobLoggerFactory = new MassiveJobs.Logging.Serilog.LoggerWrapperFactory();
    });

    if (startWorkers)
    {
        RunWorker();
    }
    else
    {
        RunPublisher();
    }
}
```
You have to implement "InitializeLogging" yourself, as you normally do initialization for your logging library. For example, for log4net this would only configure console appender.

```csharp
private static void InitializeLogging()
{
    var patternLayout = new PatternLayout();
    patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
    patternLayout.ActivateOptions();

    var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetExecutingAssembly());
    hierarchy.Root.AddAppender(new ConsoleAppender { Layout = patternLayout });

    hierarchy.Root.Level = Level.Debug;
    hierarchy.Configured = true;
}
```
Now when you start the worker application you should see logging messages in the console:
```powershell
PS> .\MassiveJobs.QuickStart.exe
2020-07-12 18:29:45,618 [1] DEBUG MassiveJobs.RabbitMqBroker.RabbitMqMessageConsumer - Connecting...
2020-07-12 18:29:45,748 [1] WARN  MassiveJobs.RabbitMqBroker.RabbitMqMessageConsumer - Connected
Initialized job worker.
Press Enter to end the application.
```
You will notice, that if you start the publisher application, it does not try to connect to RabbitMQ until you try to send the first messages. This is is because every MassiveJobs application maintains two connections to the RabbitMQ, one for publishing and the other for consuming messages. In the publisher, we are not starting workers, so consuming connection is not initialized.

```powershell
PS> .\MassiveJobs.QuickStart.exe publisher
Initialized publisher.
Write a message and press Enter to publish it (empty message to end).
> Hello
2020-07-12 18:30:27,196 [4] DEBUG MassiveJobs.RabbitMqBroker.RabbitMqMessagePublisher - Connecting...
2020-07-12 18:30:27,325 [4] WARN  MassiveJobs.RabbitMqBroker.RabbitMqMessagePublisher - Connected
```

## Using RabbitMqBroker for MassiveJobs in ASP.NET Core or Worker Service

To use `MassiveJobs.RabbitMqBroker` in a .NET Core hosted environment (ASP.NET Core, Worker Services) install the following package in your application:

```powershell
dotnet add package MassiveJobs.RabbitMqBroker.Hosting
```

Then, in your startup class, when configuring services, call `services.AddMassiveJobs()` and that's it. MassiveJobs workers will be started as a background service and you can call `Publish` on your job classes:

```csharp
//...
using MassiveJobs.RabbitMqBroker.Hosting;

namespace MassiveJobs.Examples.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            //...
            services.AddMassiveJobs();
        }

        //...
    }
}
```

For example, if you have a `Customer` entity and want to send a welcome email to a newly created customer, you might have something like this:

```csharp
// POST: api/Customers
[HttpPost]
public ActionResult<Customer> PostCustomer(Customer customer)
{
    using var trans = _context.Database.BeginTransaction();

    _context.Customers.Add(customer);

    _context.SaveChanges();

    if (!string.IsNullOrWhiteSpace(customer.Email))
    {
        // send a welcome email after 5 seconds
        SendWelcomeEmailJob.Publish(customer.Id, TimeSpan.FromSeconds(5));
    }

    // do this last. If Job publishing to RabbitMq fails, we will rollback
    trans.Commit();

    return CreatedAtAction("GetCustomer", new {id = customer.Id}, customer);
}
```

It is very important to keep in mind that `SendWelcomeEmailJob.Publish` __does NOT participate in the transaction___. 
RabbitMqBroker for MassiveJobs does not support transactions. But, the `Publish` method will throw exception if publishing fails
(only publishing - not actually sending the mail, which is done asyncronously). If the publishing fails, exception will be thrown, 
and `trans.Commit()` will never be called, and the transaction will be rolled-back on dispose.
  
Esentially, publishing a job is here used as a _last committing resource_. 

The `SendWelcomeEmailJob` could look something like this:

```csharp
public class SendWelcomeEmailJob : Job<SendWelcomeEmailJob, int>
{
    private readonly ExamplesDbContext _context;

    public SendWelcomeEmailJob(ExamplesDbContext context)
    {
        _context = context;
    }

    public override void Perform(int customerId)
    {
        using var trans = _context.Database.BeginTransaction();

        var customer = _context.Customers.Find(customerId);
        if (customer.IsEmailSent) return; // make the job idempotent

        customer.IsEmailSent = true;

        // Do this before sending email, to lessen the chance of an exception on commit.
        // Also, if optimistic concurrency is enabled, we will fail here, before sending the email.
        // This way we avoid sending the email to the customer twice.
        _context.SaveChanges();

        SendEmail(customer);

        // Do this last. In case the SendEmail method fails, the transaction will be rolled back.
        trans.Commit();
    }

    private static void SendEmail(Customer customer)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress("do-not-reply@examples.com"),
            Body = $"Welcome customer {customer.FirstName} {customer.LastName}",
            Subject = "Welcome to examples.com"
        };

        mailMessage.To.Add(customer.Email);

        using (var client = new SmtpClient("smtp.examples.com"))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential("username", "password");
            client.Send(mailMessage);
        }
    }
}
```

There are a several things to note here:
* In the hosting environment, job classes can have their required services injected in the constructor (like DbContext here)
* Since mail servers don't participate in transactions, sending email is again used as the _last committing resource_.
* __It is essential for the job classes to be idempotent__. That is why `customer.IsEmailSent` is checked before doing anything. If it is set to true we don't do anything (no exception is thrown, because exception would make the MassiveJobs library schedule the job for retries) 
* We are calling `SaveChanges()` on the db context __before__ actually sending the email so that it can __throw concurrency exceptions__ which will reschedule the job for later (but __you must configure concurrency properties on your entites__ for it to work). 
  
However, in this particular case, our job class is not fully idempotent. It still may happen that the email is sent twice because 
email server does not participate in the transaction. If `client.Send` throws __timeout__ exception, it is uncertain if the email 
was actually sent or not. Mail server might have received the request, queued the message for delivery, but we never got the response 
because of a temporary network issue. In another words, _at least once_ delivery is guaranteed in this case, not _exactly once_.
  
If __only database changes__ were involved in the job, then we could have _exactly once_ guarantees. But even then, the job's 
`Perform` method can be called twice so you __must make sure that the job is idempotent__ in the `Perform` method (similar to what we did 
with `IsEmailSent`).

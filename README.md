# MassiveJobs.RabbitMqBroker
Open source library for publishing scheduled and "out-of-band" jobs using RabbitMQ message broker. Published jobs can be performed by multiple workers distributed across multiple machines.

## Quick Start
### 1. Start RabbitMQ

If you don't have an existing installation of RabbitMQ, the simplest way is to start it in a container. 
The following command will start RabbitMq in a container __that will be immediately removed when stoped__.

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
    /// It will be instantiated every time a message is received (so make it lightweight).
    /// The class must have one method called Perform with either one of these signatures:
    /// 
    /// void Perform(TJobArg arg)
    /// Task Perform(TJobArg arg, CancellationToken cancellationToken)
    /// 
    /// We are not using aysnc calls so we will use the first option.
    /// </summary>
    public class MessageReceiver
    {
        public void Perform(string message)
        {
            Console.WriteLine("Message Received: " + message);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var rmqSettings = new RabbitMqSettings
            {
                HostNames = new[] { "localhost" },
                VirtualHost = "/",
                Username = "guest",
                Password = "guest"
            };

            var builder = RabbitMqPublisherBuilder.FromSettings(rmqSettings);

            using (var publisher = builder.Build())
            {
                if (args.Length > 0 && args[0].ToLower() == "publisher")
                {
                    Console.WriteLine("Started publisher.");
                    Console.WriteLine("Write a message and press Enter to publish it (empty message to end).");

                    while (true)
                    {
                        Console.Write("> ");
                        var message = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(message)) break;

                        // On publish we must specify the job type (MessageReceiver) 
                        // and the argument type (string).

                        publisher.Publish<MessageReceiver, string>(message);
                    }
                }
                else
                {
                    publisher.StartJobWorkers();

                    Console.WriteLine("Started job worker.");
                    Console.WriteLine("Press Enter to end the application.");

                    Console.ReadLine();
                }
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
As you enter messages in the publisher console, you will notice them being processed in one of the workers.
This is because jobs are distributed between the workers.
  
Note that you can start multiple publishers too. 
Workers and publishers can be on different machines, as long as they can access the RabbitMQ server.

## Official Documentation
TODO :/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formix.Semaphore.Tests
{
    [TestClass]
    public class SemaphoreTests
    {
        private static Random _rnd = new Random();

        [TestMethod]
        public void TestInstanceReutilization()
        {
            var semaphore1 = Semaphore.Initialize("test", 5);
            var semaphore2 = Semaphore.Initialize("test", 5);
            Assert.AreEqual(semaphore1, semaphore2);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestZeroQuantityInitialization()
        {
            Semaphore.Initialize("noname", 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestNegativeQuantityInitialization()
        {
            Semaphore.Initialize("noname", -5);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestReuseWithWrongQuantityInitialization()
        {
            var semaphore1 = Semaphore.Initialize("test", 5);
            var semaphore2 = Semaphore.Initialize("test", 6);
        }

        [TestMethod]
        public void TestMutexCriticalSection()
        {
            var itemList = new List<int>(2);
            var mutex = Semaphore.Initialize();

            var task1 = Task.Run(() =>
            {
                var token1 = new Token();
                mutex.Wait(token1);
                Task.Delay(50).Wait();
                itemList.Add(1);
                mutex.Signal(token1);
            });

            var task2 = Task.Run(() =>
            {
                Task.Delay(10).Wait();
                var token2 = new Token();
                mutex.Wait(token2);
                itemList.Add(2);
                Task.Delay(25).Wait();
                mutex.Signal(token2);
            });

            Task.WaitAll(new[] { task1, task2 }, 100000);

            Assert.IsTrue(task1.IsCompleted, "Task1 did not complete!");
            Assert.IsTrue(task2.IsCompleted, "Task2 did not complete!");

            Assert.AreEqual(1, itemList[0]);
            Assert.AreEqual(2, itemList[1]);
        }

        [TestMethod]
        public async Task TestWithThreeTasks()
        {
            await ExecuteThreeTasks();
        }

        private async Task ExecuteThreeTasks()
        {
            var semaphore = Semaphore.Initialize("connections", 2);

            // Lets these tasks execute asynchronously
            var task1 = Task.Run(() =>
            {
                var token1 = new Token();
                semaphore.Wait(token1);
                Console.WriteLine("Task 1 started.");
                Task.Delay(250).Wait();
                Console.WriteLine("Task 1 done.");
                semaphore.Signal(token1);
            });

            var task2 = Task.Run(() =>
            {
                var token2 = new Token();
                semaphore.Wait(token2);
                Console.WriteLine("Task 2 started.");
                Task.Delay(500).Wait();
                Console.WriteLine("Task 2 done.");
                semaphore.Signal(token2);
            });

            var task3 = Task.Run(() =>
            {
                var token3 = new Token();
                semaphore.Wait(token3);
                Console.WriteLine("Task 3 started.");
                Task.Delay(350).Wait();
                Console.WriteLine("Task 3 done.");
                semaphore.Signal(token3);
            });

            Task.WaitAll(new[] { task1, task2, task3 }, 2000);
            Assert.IsTrue(task1.IsCompleted, "Task1 did not complete.");
            Assert.IsTrue(task2.IsCompleted, "Task2 did not complete.");
            Assert.IsTrue(task3.IsCompleted, "Task3 did not complete.");

            await Task.CompletedTask;
        }


        [TestMethod]
        public void TestRunningALotOfTasks()
        {
            const int taskCount = 10;
            var tasks = new List<Task>(taskCount + 1);
            var taskFinished = new bool[taskCount];
            
            // Seeding random to something that looks like a good set of values to me
            var rnd = new Random(2);

            // Initialize the semaphore with a random value.
            var value = rnd.Next(10) + 3;
            var semaphore = Semaphore.Initialize("TestRunningALotOfTasks", value);
            semaphore.Delay = 3;
            Console.WriteLine($"*** Semaphore Created. Value = {value} ***");
            var start = DateTime.Now.Ticks / 10000;

            // Create dummy tasks and starts them
            for (int i = 0; i < taskCount; i++)
            {
                var index = i; // Store 'i' value in a local variable for later use in  lambda expression
                
                // Randomize the semaphore usage for the task that will be started.
                var token = new Token(rnd.Next(value) + 1);

                tasks.Add(Task.Run(() =>
                {
                    // This is the fake task code...
                    semaphore.Wait(token);
                    var elapsed = DateTime.Now.Ticks / 10000 - start;
                    Console.WriteLine($"[{elapsed}] Task {index}, usage {token.Usage}, Started");
                    Task.Delay(rnd.Next(40) + 10).Wait();
                    Console.WriteLine($"[{elapsed}] Task {index}, usage {token.Usage}, Running");
                    Task.Delay(rnd.Next(40) + 10).Wait();
                    Console.WriteLine($"[{elapsed}] Task {index}, usage {token.Usage}, Done");
                    taskFinished[index] = true;
                    Task.Delay(rnd.Next(40) + 10).Wait();
                    semaphore.Signal(token);
                }));

                Console.WriteLine($"- Task {index} created, Usage = {token.Usage}");
            }

            // Creates a task to monitor all the other tasks
            var monitoringTask = Task.Run(async () =>
            {
                var semaphoreStatus = new Dictionary<string, int>()
                {
                    { "TotalTasksCount", 0},
                    { "RunningTasksCount", 0},
                    { "RunningTasksUsage", 0},
                };

                while (semaphore.Tokens.Count() > 0)
                {
                    // Make sure that no task overrun the semaphore value.
                    var totalUsage = 0;
                    lock(semaphore.Tokens)
                    {
                        totalUsage = semaphore.Tokens
                            .Where(t => t.IsRunning)
                            .Sum(t => t.Usage);

                        Assert.IsTrue(semaphore.Value >= totalUsage);
                        PrintSemaphoreStatus(semaphoreStatus, semaphore);
                    }
                    await Task.Delay(5);
                }

                PrintSemaphoreStatus(semaphoreStatus, semaphore);

                lock (semaphore.Tokens)
                {
                    var samaphoreTasksCount = semaphore.Tokens.Count();
                    var semaphoreRunningTasksCount = semaphore.Tokens.Where(t => t.IsRunning).Count();
                    var semaphoreRunningTasksUsage = semaphore.Tokens.Where(t => t.IsRunning).Sum(t => t.Usage);

                    Assert.AreEqual(0, samaphoreTasksCount);
                    Assert.AreEqual(0, semaphoreRunningTasksCount);
                    Assert.AreEqual(0, semaphoreRunningTasksUsage);
                }
            });

            // Adds the monitoring task to the batch and await them all
            tasks.Add(monitoringTask);
            Task.WaitAll(tasks.ToArray());

            foreach (var taskDone in taskFinished)
            {       
                Assert.IsTrue(taskDone);
            }
        }

        private void PrintSemaphoreStatus(
            Dictionary<string, int> semaphoreStatuses, Semaphore semaphore)
        {
            lock (semaphore.Tokens)
            {
                var samaphoreTasksCount = semaphore.Tokens.Count();
                var semaphoreRunningTasksCount = semaphore.Tokens.Where(t => t.IsRunning).Count();
                var semaphoreRunningTasksUsage = semaphore.Tokens.Where(t => t.IsRunning).Sum(t => t.Usage);

                if (semaphoreStatuses["TotalTasksCount"] != samaphoreTasksCount)
                {
                    Console.WriteLine($"TotalTasksCount: {samaphoreTasksCount}");
                    semaphoreStatuses["TotalTasksCount"] = samaphoreTasksCount;
                }

                if (semaphoreStatuses["RunningTasksCount"] != semaphoreRunningTasksCount)
                {
                    Console.WriteLine($"RunningTasksCount: {semaphoreRunningTasksCount}");
                    semaphoreStatuses["RunningTasksCount"] = semaphoreRunningTasksCount;
                }

                if (semaphoreStatuses["RunningTasksUsage"] != semaphoreRunningTasksUsage)
                {
                    Console.WriteLine($"RunningTasksUsage: {semaphoreRunningTasksUsage}/{semaphore.Value}");
                    semaphoreStatuses["RunningTasksUsage"] = semaphoreRunningTasksUsage;
                }
            }
        }




    }
}

﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formix.Utilities.Synchronization
{
    public class Semaphore : AbstractSemaphore
    {
        #region static members
        private static readonly IDictionary<string, Semaphore> _semaphores;

        static Semaphore()
        {
            _semaphores = new Dictionary<string, Semaphore>();
        }
        #endregion


        private LinkedList<SemaphoreTask> _semaphoreEntries;


        private Semaphore(string name, int quantity)
        {
            Name = name;
            Quantity = quantity;
            _semaphoreEntries = new LinkedList<SemaphoreTask>();
        }


        public static Semaphore Initialize(string name, int quantity)
        {
            lock(_semaphores)
            {
                if (!_semaphores.ContainsKey(name))
                {
                    _semaphores.Add(name,
                        new Semaphore(name, quantity));
                }

                var semaphore = _semaphores[name];
                if (semaphore.Quantity != quantity)
                {
                    throw new InvalidOperationException(
                        $"You cannot initialize the semaphore {name} " +
                            $"with a quantity of {quantity}. That " +
                            $"semaphore already exist with a different " +
                            $"quantity: {semaphore.Quantity}.");
                }

                return semaphore;
            }
        }

        protected override async Task Enqueue(SemaphoreTask entry)
        {
            lock (_semaphoreEntries)
            {
                _semaphoreEntries.AddLast(entry);
            }
            await Task.CompletedTask;
        }

        protected override async Task Dequeue(SemaphoreTask entry)
        {
            await Task.Run(() =>
            {
                lock (_semaphoreEntries)
                {
                    _semaphoreEntries.Remove(entry);
                }
            });
        }

        protected override async Task<bool> CanExecute(SemaphoreTask entry)
        {
            return await Task.Run(() =>
            {
                lock (_semaphoreEntries)
                {
                    int remains = Quantity;
                    foreach (var e in _semaphoreEntries)
                    {
                        if (e == entry && remains - entry.Usage >= 0)
                        {
                            return true;
                        }

                        remains -= e.Usage;
                        if (remains <= 0)
                        {
                            return false;
                        }
                    }
                }
                return false;
            });
        }
    }
}

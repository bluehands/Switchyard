using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Switchyard.CodeGeneration.Test
{
	public abstract class TestSpecification
    {
        [TestInitialize]
        public void Init()
        {
            Given();
            When();
        }

        protected virtual void Given() { }
        protected abstract void When();
    }

    [TestClass]
    public abstract class AsyncTestSpecification : VerifyCSharpSource
    {
        [TestInitialize]
        public async Task Initialize()
        {
            Given();
            await GivenAsync().ConfigureAwait(false);
            await When().ConfigureAwait(false);
        }

        protected virtual Task GivenAsync() => Task.FromResult(42);

        protected virtual void Given() { }
        protected virtual Task When() => Task.FromResult(42);

        [TestCleanup]
        public virtual void CleanUp() { }

        protected void Log(string message)
        {
            Logger.Log(message);
        }
    }

    public static class Logger
    {
        public static void Log(object message)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss fff} ({Thread.CurrentThread.ManagedThreadId:000}): {message}");
        }
    }
}
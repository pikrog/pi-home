using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PiHome
{
    class RepeatingTask
    {
        public async static Task Run(Func<Task> task, TimeSpan interval, CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                await task();
                await Task.Delay((int)interval.TotalMilliseconds, token);
            }
            throw new TaskCanceledException();
        }

        public async static Task Run(Action task, TimeSpan interval, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                task();
                await Task.Delay((int)interval.TotalMilliseconds, token);
            }
            throw new TaskCanceledException();
        }
    }
}

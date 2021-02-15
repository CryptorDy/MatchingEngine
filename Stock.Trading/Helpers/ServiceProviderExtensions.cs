using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MatchingEngine.Helpers
{
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Create instance using DI services
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="lastArgs">additional arguments (should be at the end of constructor, in the same order)</param>
        public static TResult CreateInstance<TResult>(this IServiceProvider provider, params object[] lastArgs)
            where TResult : class
        {
            ConstructorInfo constructor = typeof(TResult).GetConstructors()[0];
            if (constructor == null)
                throw new ArgumentException($"No constructor in type {typeof(TResult)}");

            var argTypes = constructor.GetParameters().Select(_ => _.ParameterType).ToArray();
            var args = new List<object>();

            int fromDICount = argTypes.Length - lastArgs.Length; // how many args should be requested from provider
            if (fromDICount < 0)
                throw new ArgumentException($"Too many lastArgs, constructor expects {argTypes.Length} args");

            for (int i = 0; i < argTypes.Length; i++)
            {
                var argType = argTypes[i];
                if (i < fromDICount) // request from provider
                {
                    args.Add(provider.GetService(argType));
                }
                else // take from lastArgs
                {
                    var arg = lastArgs[i - fromDICount];
                    if (arg != null && !argType.IsAssignableFrom(arg.GetType())) // check type compatibility
                        throw new ArgumentException($"Can't assign {arg.GetType()} to {argType} (arg number:{i}, value:{arg})");
                    args.Add(arg);
                }
            }
            var instance = Activator.CreateInstance(typeof(TResult), args.ToArray()) as TResult;
            return instance;
        }
    }
}

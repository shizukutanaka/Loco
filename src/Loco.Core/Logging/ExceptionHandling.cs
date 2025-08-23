using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging
{
    /// <summary>
    /// Utility class for standardized exception handling
    /// </summary>
    public static class ExceptionHandling
    {
        /// <summary>
        /// Handles an exception with logging
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="exception">Exception to handle</param>
        /// <param name="context">Context where the exception occurred</param>
        /// <param name="level">Log level (defaults to Error)</param>
        public static void HandleException(ILogger logger, Exception exception, string context, LogLevel level = LogLevel.Error)
        {
            if (logger == null || exception == null) return;
            
            logger.Log(level, exception, "Exception in {Context}: {Message}", context, exception.Message);
        }
        
        /// <summary>
        /// Handles an exception with logging and returns a default value
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="logger">Logger instance</param>
        /// <param name="exception">Exception to handle</param>
        /// <param name="context">Context where the exception occurred</param>
        /// <param name="defaultValue">Default value to return</param>
        /// <param name="level">Log level (defaults to Error)</param>
        /// <returns>Default value</returns>
        public static T HandleException<T>(ILogger logger, Exception exception, string context, T defaultValue = default, LogLevel level = LogLevel.Error)
        {
            HandleException(logger, exception, context, level);
            return defaultValue;
        }
        
        /// <summary>
        /// Executes an action with exception handling
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="context">Context where the action is executed</param>
        /// <param name="level">Log level for exceptions (defaults to Error)</param>
        public static void SafeExecute(Action action, ILogger logger, string context, LogLevel level = LogLevel.Error)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                HandleException(logger, ex, context, level);
            }
        }
        
        /// <summary>
        /// Executes a function with exception handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="func">Function to execute</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="context">Context where the function is executed</param>
        /// <param name="defaultValue">Default value to return in case of exception</param>
        /// <param name="level">Log level for exceptions (defaults to Error)</param>
        /// <returns>Function result or default value if exception occurs</returns>
        public static T SafeExecute<T>(Func<T> func, ILogger logger, string context, T defaultValue = default, LogLevel level = LogLevel.Error)
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception ex)
            {
                return HandleException(logger, ex, context, defaultValue, level);
            }
        }
        
        /// <summary>
        /// Executes an async task with exception handling
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="context">Context where the task is executed</param>
        /// <param name="level">Log level for exceptions (defaults to Error)</param>
        /// <returns>Task result</returns>
        public static async Task SafeExecuteAsync(Func<Task> task, ILogger logger, string context, LogLevel level = LogLevel.Error)
        {
            try
            {
                if (task != null)
                    await task();
            }
            catch (Exception ex)
            {
                HandleException(logger, ex, context, level);
            }
        }
        
        /// <summary>
        /// Executes an async function with exception handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="func">Function to execute</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="context">Context where the function is executed</param>
        /// <param name="defaultValue">Default value to return in case of exception</param>
        /// <param name="level">Log level for exceptions (defaults to Error)</param>
        /// <returns>Function result or default value if exception occurs</returns>
        public static async Task<T> SafeExecuteAsync<T>(Func<Task<T>> func, ILogger logger, string context, T defaultValue = default, LogLevel level = LogLevel.Error)
        {
            try
            {
                return func != null ? await func() : defaultValue;
            }
            catch (Exception ex)
            {
                return HandleException(logger, ex, context, defaultValue, level);
            }
        }
    }
}

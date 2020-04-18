using System.Diagnostics.CodeAnalysis;

namespace QBDiscordAssistant
{
    [SuppressMessage(
        "Design", 
        "CA1000:Do not declare static members on generic types", 
        Justification = "Prefer this to static factory class to create it.")]
    public class Result<T>
    {
        private Result(bool success, T value, string errorMessage)
        {
            this.Success = success;
            this.Value = value;
            this.ErrorMessage = errorMessage;
        }

        public bool Success { get; }

        public T Value { get; }

        public string ErrorMessage { get; }

        public static Result<T> CreateSuccessResult(T value)
        {
            return new Result<T>(true, value, null);
        }

        public static Result<T> CreateFailureResult(string errorMessage = null)
        {
            return new Result<T>(false, default(T), errorMessage);
        }
    }
}

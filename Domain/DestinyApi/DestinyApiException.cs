namespace Domain.DestinyApi
{
    public class DestinyApiException : Exception
    {
        public DestinyApiException()
        {
        }

        public DestinyApiException(string message)
            : base(message)
        {
        }

        public DestinyApiException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public int ErrorCode { get; }

        public string ErrorStatus { get; }

        public string ErrorMessage { get; }

        public DestinyApiResponseError? Error { get; }

        public DestinyApiException(DestinyApiResponseError error)
            : base(FormatMessage(error))
        {
            ErrorCode = error.ErrorCode;
            ErrorStatus = error.ErrorStatus;
            ErrorMessage = error.Message;
            Error = error;
        }

        private static string FormatMessage(DestinyApiResponseError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return $"Destiny API error {error.ErrorCode} ({error.ErrorStatus}): {error.Message}";
        }
    }
}

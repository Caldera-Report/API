namespace Domain.DestinyApi
{
    public class DestinyApiResponse<T>
    {
        public required T Response { get; set; }
        public int ErrorCode { get; set; }
        public int ThrottleSeconds { get; set; }
        public required string ErrorStatus { get; set; }
        public required string Message { get; set; }
        public required Dictionary<string, string> MessageData { get; set; }
    }

    public class DestinyApiResponseError
    {
        public int ErrorCode { get; set; }
        public int ThrottleSeconds { get; set; }
        public required string ErrorStatus { get; set; }
        public required string Message { get; set; }
        public required Dictionary<string, string> MessageData { get; set; }
    }
}

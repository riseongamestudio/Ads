namespace RiseOn.Ads {
    public readonly struct AdError {
        public readonly int    Code;
        public readonly string Message;

        public AdError(int code, string message) {
            Code    = code;
            Message = message;
        }

        public override string ToString() => $"{Code}: {Message}";
    }
}
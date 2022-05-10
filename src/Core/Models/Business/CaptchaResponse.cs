namespace Bit.Core.Models.Business
{
    public class CaptchaResponse
    {
        public bool Success { get; set; }
        public bool MaybeBot { get; set; }
        public bool IsBot { get; set; }
    }
}

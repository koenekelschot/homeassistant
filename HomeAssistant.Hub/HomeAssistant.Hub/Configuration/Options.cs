namespace HomeAssistant.Hub.Configuration
{
    public class Options<TOptions> : IOptions<TOptions> where TOptions : class, new()
    {
        private readonly TOptions _value;

        internal Options(TOptions value) {
            _value = value;
        }

        public TOptions Value => _value;
    }
}

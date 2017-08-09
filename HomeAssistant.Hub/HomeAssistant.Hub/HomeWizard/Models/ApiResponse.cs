namespace HomeAssistant.Hub.HomeWizard.Models
{
    public class ApiResponse<T>
    {
        public string Status { get; set; }
        public string Version { get; set; }
        public ApiRequest Request { get; set; }
        public T Response { get; set; }
    }

    public class ApiRequest
    {
        public string Route { get; set; }
    }
}

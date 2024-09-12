using Prometheus;
using GuacamoleExporter.ExternalServices.GuacamoleApiService;
using GuacamoleExporter.ExternalServices.GuacamoleApiService.Models;

public class Program : IDisposable
{
    private System.Timers.Timer _timer;

    private MetricServer _metricsServer;
    private Gauge _guacamoleUpMetrics;
    private Gauge _guacamoleCountOfUserMetrics;
    private Gauge _guacamoleCountOfActiveConnectionMetrics;
    private Gauge _guacamoleActiveConnection;

    private GuacamoleApiService _guacamoleApiService;

    private double _timeShift;

    public Program(int listenPort, int intervalCheck, string guacamoleUsername, string guacamolePassword, string? guacamoleDatasource, string guacamoleHostname, double timeShift)
    {
        _timeShift = timeShift;

        _timer = new System.Timers.Timer(intervalCheck * 1000);
        _timer.Elapsed += async (sender, e) => await UpdateMetricsAsync();
        _timer.AutoReset = true;
        _timer.Enabled = true;

        _metricsServer = new MetricServer(port: listenPort);
        _metricsServer.Start();

        Metrics.SuppressDefaultMetrics();
        _guacamoleUpMetrics = Metrics.CreateGauge("guacamole_up", "Indicates whether guacamole is up");
        _guacamoleCountOfUserMetrics = Metrics.CreateGauge("guacamole_count_of_user", "Indicates count of user");
        _guacamoleCountOfActiveConnectionMetrics = Metrics.CreateGauge("guacamole_count_of_active_connection", "Count of active connection");
        _guacamoleActiveConnection = Metrics.CreateGauge("guacamole_active_connection", "Active connections", new[] { "user", "startDateUtc", "startDate_str", "startDate_order", "connected_since", "connectionIdentifier", "connectionName", "connectionProtocol" });

        _guacamoleApiService = new GuacamoleApiService(guacamoleHostname, guacamoleUsername, guacamolePassword, guacamoleDatasource);

        Task.Run(async () => await UpdateMetricsAsync());
    }

    private async Task UpdateMetricsAsync()
    {
        try
        {
            bool guacIsUp = false;
            try
            {
                guacIsUp = await _guacamoleApiService.GetGuacIsUp();
            }
            catch (Exception)
            {
                guacIsUp = false;
            }

            _guacamoleUpMetrics.Set(guacIsUp ? 1 : 0);

            if (guacIsUp)
            {
                int countOfUser = await _guacamoleApiService.GetCountOfUser();

                List<ActiveConnection> activeConnections = await _guacamoleApiService.GetActiveConnection();
                int countOfActiveConnection = activeConnections.Count;

                _guacamoleCountOfUserMetrics.Set(countOfUser);
                _guacamoleCountOfActiveConnectionMetrics.Set(countOfActiveConnection);

                _guacamoleActiveConnection.GetAllLabelValues().ToList().ForEach(x => _guacamoleActiveConnection.RemoveLabelled(x));

                foreach (ActiveConnection connection in activeConnections)
                {
                    connection.StartDate = connection.StartDate.AddHours(_timeShift);

                    TimeSpan dif = DateTime.Now - connection.StartDate;
                    dif = new TimeSpan(dif.Hours, dif.Minutes, dif.Seconds);

                    _guacamoleActiveConnection.WithLabels(connection.Username, 
                        connection.StartDate.ToFileTimeUtc().ToString(), 
                        connection.StartDate.ToString("G"),
                        connection.StartDate.ToString("yyyyMMddHHmmss"),
                        Convert.ToInt32(dif.TotalMinutes).ToString(),
                        connection.ConnectionIdentifier, 
                        connection.ConnectionName, 
                        connection.ConnectionProtocol)
                        .Set(1);

                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred in UpdateMetricsAsync: {ex}");
        }
    }

    private static void Main(string[] args)
    {
        int listenPort = int.Parse(Environment.GetEnvironmentVariable("LISTEN_PORT") ?? "9134");
        int intervalCheck = int.Parse(Environment.GetEnvironmentVariable("INTERVAL_CHECK") ?? "30");
        string guacamoleUsername = Environment.GetEnvironmentVariable("GUACAMOLE_USERNAME") ?? throw new ArgumentNullException("GUACAMOLE_USERNAME argument is missing");
        string guacamolePassword = Environment.GetEnvironmentVariable("GUACAMOLE_PASSWORD") ?? throw new ArgumentNullException("GUACAMOLE_PASSWORD argument is missing");
        string? guacamoleDatasource = Environment.GetEnvironmentVariable("GUACAMOLE_DATASOURCE") ?? throw new ArgumentNullException("GUACAMOLE_DATASOURCE argument is missing");
        string guacamoleHostname = Environment.GetEnvironmentVariable("GUACAMOLE_HOSTNAME") ?? throw new ArgumentNullException("GUACAMOLE_HOSTNAME argument is missing");
        double timeShift = double.Parse(Environment.GetEnvironmentVariable("TIME_SHIFT") ?? "0");

        new Program(listenPort, intervalCheck, guacamoleUsername, guacamolePassword, guacamoleDatasource, guacamoleHostname, timeShift);

        while (true) { Console.ReadLine(); }
    }

    public void Dispose()
    {
        _metricsServer.Dispose();
    }
}

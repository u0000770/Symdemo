using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

static async Task Main(string[] args)
{
    var client = new HttpClient { BaseAddress = new Uri("https://localhost:7021/") };

    const string apiKey = "u0000770"; // Replace with your actual API key

    // Add the API key to the default request headers
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

    double targetHigh = 19.0; // Target upper temperature
    double targetLow = 18.0;  // Target lower temperature
    bool heating = true;      // Flag to toggle heating or cooling

    Console.WriteLine("Starting temperature control simulation...");

    while (true)
    {
        await Task.Delay(1000);
        try
        {
            // Read current temperature
            double currentTemperature = await GetSensorTemperature(client, 1);
            Console.WriteLine($"Current Temperature: {currentTemperature:F1}°C");

            // Read current system state
            var systemState = await GetSystemState(client);
            Console.WriteLine("System State:");
            foreach (var heater in systemState.Heaters)
            {
                Console.WriteLine($"  Heater {heater.HeaterId}: Level {heater.Level}");
            }
            foreach (var fan in systemState.Fans)
            {
                Console.WriteLine($"  Fan {fan.FanId}: {(fan.IsOn ? "On" : "Off")}");
            }

            // NEW: Print environment configurations for visibility
            var sensorConfigs = await GetSensorConfigurations(client);
            Console.WriteLine("Sensor Configurations:");
            foreach (var config in sensorConfigs)
            {
                Console.WriteLine($"  Sensor {config.Id}: Adjustment Logic - {config.LogicDescription}");
            }

            var fanConfigs = await GetFanConfigurations(client);
            Console.WriteLine("Fan Configurations:");
            foreach (var config in fanConfigs)
            {
                Console.WriteLine($"  Fan {config.Id}: Delay - {config.DelaySeconds} seconds");
            }

            // Control heating or cooling
            if (heating)
            {
                // If heating and below targetHigh, turn on heater
                if (currentTemperature < targetHigh)
                {
                    Console.WriteLine("Heating up...");
                    for (int i = 1; i <= 3; i++)
                    {
                        await SetHeaterLevel(client, i, 5); // Turn heaters to max
                    }
                }
                else
                {
                    Console.WriteLine("Target temperature reached. Turning off heater.");
                    for (int i = 1; i <= 3; i++)
                    {
                        await SetHeaterLevel(client, i, 0); // Turn off heaters
                    }
                    heating = false; // Switch to cooling
                }
            }
            else
            {
                // If cooling and above targetLow, turn on fans
                if (currentTemperature > targetLow)
                {
                    Console.WriteLine("Cooling down...");
                    for (int i = 1; i <= 3; i++)
                    {
                        await SetFanState(client, i, true); // Turn on fans
                    }
                }
                else
                {
                    Console.WriteLine("Minimum temperature reached. Turning off fans.");
                    for (int i = 1; i <= 3; i++)
                    {
                        await SetFanState(client, i, false); // Turn off fans
                    }
                    heating = true; // Switch to heating
                }
            }

            // Wait 1 second before next reading
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

static async Task<double> GetSensorTemperature(HttpClient client, int sensorId)
{
    var response = await client.GetAsync($"api/sensor/{sensorId}");
    if (response.IsSuccessStatusCode)
    {
        var tempString = await response.Content.ReadAsStringAsync();
        return double.Parse(tempString);
    }

    throw new Exception($"Failed to get temperature from sensor {sensorId}: {response.ReasonPhrase}");
}

static async Task<SystemStateDTO> GetSystemState(HttpClient client)
{
    var response = await client.GetAsync("api/SystemState");
    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SystemStateDTO>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    throw new Exception($"Failed to get system state: {response.ReasonPhrase}");
}

static async Task SetHeaterLevel(HttpClient client, int heaterId, int level)
{
    var response = await client.PostAsync($"api/heat/{heaterId}",
        new StringContent(level.ToString(), System.Text.Encoding.UTF8, "application/json"));
    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"Failed to set heater level {heaterId}: {response.ReasonPhrase}");
    }
}

static async Task SetFanState(HttpClient client, int fanId, bool isOn)
{
    var response = await client.PostAsync($"api/fans/{fanId}",
        new StringContent(isOn.ToString().ToLower(), System.Text.Encoding.UTF8, "application/json"));
    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"Failed to set fan state for fan {fanId}: {response.ReasonPhrase}");
    }
}

// NEW: Fetch sensor configurations from the API
static async Task<SensorConfigDTO[]> GetSensorConfigurations(HttpClient client)
{
    var response = await client.GetAsync("api/sensors/configurations");
    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SensorConfigDTO[]>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    throw new Exception($"Failed to get sensor configurations: {response.ReasonPhrase}");
}

// NEW: Fetch fan configurations from the API
static async Task<FanConfigDTO[]> GetFanConfigurations(HttpClient client)
{
    var response = await client.GetAsync("api/fans/configurations");
    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<FanConfigDTO[]>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    throw new Exception($"Failed to get fan configurations: {response.ReasonPhrase}");
}
}

public class SystemStateDTO
{
    public HeaterDTO[] Heaters { get; set; }
    public FanDTO[] Fans { get; set; }
}

public class HeaterDTO
{
    public int HeaterId { get; set; }
    public int Level { get; set; }
}

public class FanDTO
{
    public int FanId { get; set; }
    public bool IsOn { get; set; }
}

// NEW: DTO for sensor configurations
public class SensorConfigDTO
{
    public int Id { get; set; }
    public string LogicDescription { get; set; }
}

// NEW: DTO for fan configurations
public class FanConfigDTO
{
    public int Id { get; set; }
    public int DelaySeconds { get; set; }
}


using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Honeycomb.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpCodes = Hybrasyl.Enums.OpCodes;

namespace Hybrasyl.Metrics
{
    public class MetricsCollector
    {
        public const string ServiceName = "erisco.hybrasyl.server";
        private HoneycombOptions options;
        private static Meter Meter { get; set; }
        private static MeterProvider MeterProvider { get; set; }

        public Dictionary<byte, Counter<long>> WorldPacketInvocations { get; set; } = new();
        public Dictionary<byte, Counter<long>> WorldPacketErrors { get; set; } = new();
        public Dictionary<byte, Counter<long>> ControlPacketInvocations { get; set; } = new();
        public Dictionary<byte, Counter<long>> ControlPacketErrors { get; set; } = new();

        public MetricsCollector(World world, string apiKey)
        {
            // HONEYCOMB
            var options = new HoneycombOptions
            {
                ServiceName = ServiceName,
                ApiKey = apiKey,
                Dataset = "hybrasyl.server",
                MetricsDataset = "hybrasyl.server",
            };

            MeterProvider ??= OpenTelemetry.Sdk.CreateMeterProviderBuilder().AddHoneycomb(options)
                .AddMeter(ServiceName).AddConsoleExporter().Build();
            
            Meter ??= new Meter(options.ServiceName);

            foreach (var (opcode, handler) in world.WorldPacketHandlers)
            {
                WorldPacketInvocations.Add(opcode, Meter.CreateCounter<long>($"{handler.Method.Name}.invocations"));
                WorldPacketErrors.Add(opcode, Meter.CreateCounter<long>($"{handler.Method.Name}.errors"));
            }
        }

        public void RecordInvocation(byte opcode, double responseTime, string user=null, string ipAddress=null)
        {
            if (WorldPacketInvocations.TryGetValue(opcode, out var counter))
            {
                counter.Add(1, 
                    new("User", user),
                    new ("RemoteAddress", ipAddress));
            }
            else
                GameLog.Error($"Opcode {opcode} unrecognized");

            MeterProvider.ForceFlush();
        }

        public void RecordError(byte opcode, string user, string ipAddress)
        {
            if (WorldPacketErrors.TryGetValue(opcode, out var counter))
            {
                counter.Add(1, 
                    new("User", user),
                    new ("RemoteAddress", ipAddress));
            }
        }


    }
}

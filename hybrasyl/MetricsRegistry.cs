using App.Metrics;
using App.Metrics.Histogram;
using App.Metrics.Meter;

namespace Hybrasyl
{
    public static class HybrasylMetricsRegistry
    {

        public static MeterOptions ExceptionMeter => new MeterOptions
        {
            Name = "Exception Rate",
            MeasurementUnit = Unit.Errors
        };

        public static MeterOptions MessageMeter => new MeterOptions
        {
            Name = "Message Count",
            MeasurementUnit = Unit.Requests
        };

        public static HistogramOptions ServiceTime => new HistogramOptions
        {
            Name = "Opcode Service Times",
            MeasurementUnit = Unit.Custom("milliseconds")
        };

    }
}

using System.Diagnostics;

namespace RZND.Bot.Tracing
{
    public static class Tracer
    {
        // Nome do seu aplicativo – usado por visualizadores (Jaeger, Zipkin, etc.)
        public static readonly ActivitySource Source = new ActivitySource("RZND.Bot");

        // Convenção: criar um método que inicia a Activity e devolve um IDisposable
        public static IDisposable? Start(string operationName)
        {
            var activity = Source.StartActivity(operationName, ActivityKind.Internal);
            return activity;
        }
    }
}

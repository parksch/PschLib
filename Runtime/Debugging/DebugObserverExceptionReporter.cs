#if UNITY_EDITOR
using System;

namespace PschLib.Debugging
{
    public static class DebugObserverExceptionReporter
    {
        private static Action<string, Exception, object> reporter = ReportToTrace;
        private static bool isReporting;

        public static void SetReporter(Action<string, Exception, object> value)
        {
            reporter = value ?? ReportToTrace;
        }

        public static void Report(string source, Exception exception, object context = null)
        {
            if (isReporting)
            {
                TryReportToTrace(source, exception);
                return;
            }

            isReporting = true;

            try
            {
                reporter(source, exception, context);
            }
            catch (Exception reporterException)
            {
                TryReportToTrace(nameof(DebugObserverExceptionReporter), reporterException);
                TryReportToTrace(source, exception);
            }
            finally
            {
                isReporting = false;
            }
        }

        private static void ReportToTrace(string source, Exception exception, object context)
        {
            System.Diagnostics.Trace.TraceError($"{source} debug listener failed: {exception}");
        }

        private static void TryReportToTrace(string source, Exception exception)
        {
            try
            {
                ReportToTrace(source, exception, null);
            }
            catch (Exception)
            {
                // Debug reporting must not affect the observed operation.
            }
        }
    }
}
#endif

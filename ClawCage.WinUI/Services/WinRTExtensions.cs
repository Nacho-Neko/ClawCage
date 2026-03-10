using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Services
{
    internal static class WinRTExtensions
    {
        internal static Task<T?> ToTask<T>(this IAsyncOperation<T> operation) where T : class
        {
            var tcs = new TaskCompletionSource<T?>();
            operation.Completed = (op, status) =>
                tcs.TrySetResult(status == AsyncStatus.Completed ? op.GetResults() : null);
            return tcs.Task;
        }

        internal static Task<T> ToTaskStruct<T>(this IAsyncOperation<T> operation) where T : struct
        {
            var tcs = new TaskCompletionSource<T>();
            operation.Completed = (op, status) =>
                tcs.TrySetResult(status == AsyncStatus.Completed ? op.GetResults() : default);
            return tcs.Task;
        }
    }
}

using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using System.Windows;

namespace Com.Josh2112.StreamIt.UI
{
    public interface IDialogWithResponse<T> {}

    public static class DialogExtensions
    {
        public static async Task<T> ShowDialogAsync<T>( this Window window, IDialogWithResponse<T> dialog ) =>
            (T)(await window.ShowDialog( dialog ))!;
    }
}

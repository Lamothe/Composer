using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Composer.UI
{
    public static class Utilities
    {
        public async static void CallUI(DispatchedHandler f)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, f);
        }

        public async static void CallUIIdle(IdleDispatchedHandler f)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunIdleAsync(f);
        }
    }
}

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
        public async static void CallUI(Action f)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunIdleAsync((a) => f());
        }
    }
}

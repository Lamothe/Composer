using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Model
{
    public class Bar
    {
        public event EventHandler Update;

        public float[] Buffer { get; set; }

        public void EmitUpdate()
        {
            Update?.Invoke(this, EventArgs.Empty);
        }

        public void SetEmpty()
        {
            Buffer = null;
        }
    }
}

using System.ComponentModel;
using System.Drawing;

namespace XerahS.Common
{
    public class MyColorConverter : ColorConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return false;
        }
    }
}

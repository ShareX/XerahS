using System.ComponentModel;
using System.Drawing;

namespace ShareX.Avalonia.Common
{
    public class MyColorConverter : ColorConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return false;
        }
    }
}

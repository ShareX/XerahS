namespace XerahS.Common
{
    public delegate void ColorEventHandler(object sender, ColorEventArgs e);

    public class ColorEventArgs : EventArgs
    {
        public ColorEventArgs(MyColor color, ColorType colorType)
        {
            Color = color;
            ColorType = colorType;
        }

        public ColorEventArgs(MyColor color, DrawStyle drawStyle)
        {
            Color = color;
            DrawStyle = drawStyle;
        }

        public MyColor Color;
        public ColorType ColorType;
        public DrawStyle DrawStyle;
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace FluentLyrics.Services
{
    public class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(16, 20, 28);
        public override Color ImageMarginGradientBegin => Color.FromArgb(16, 20, 28);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(16, 20, 28);
        public override Color ImageMarginGradientEnd => Color.FromArgb(16, 20, 28);
        public override Color MenuBorder => Color.FromArgb(37, 45, 61);
        public override Color MenuItemBorder => Color.FromArgb(0, 240, 255);
        public override Color MenuItemSelected => Color.FromArgb(28, 38, 54);
        public override Color MenuStripGradientBegin => Color.FromArgb(16, 20, 28);
        public override Color MenuStripGradientEnd => Color.FromArgb(16, 20, 28);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(28, 38, 54);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(28, 38, 54);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(20, 28, 42);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(20, 28, 42);
        public override Color SeparatorDark => Color.FromArgb(37, 45, 61);
        public override Color SeparatorLight => Color.Transparent;
        public override Color CheckBackground => Color.FromArgb(20, 45, 55);
        public override Color CheckSelectedBackground => Color.FromArgb(0, 240, 255);
        public override Color CheckPressedBackground => Color.FromArgb(0, 240, 255);
    }
}

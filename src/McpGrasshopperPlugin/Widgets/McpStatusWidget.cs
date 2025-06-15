using System;
using System.Drawing;
using System.IO;
using Svg;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace McpGrasshopperPlugin.Widgets
{
    /// <summary>
    /// Simple UI widget that shows an MCP icon in the bottom right corner of the Grasshopper canvas
    /// and displays transient chat bubbles with status messages.
    /// </summary>
    public class McpStatusWidget : GH_AssemblyPriority
    {
        private static PictureBox? _iconBox;
        private static Label? _bubbleLabel;

        public override GH_LoadingInstruction PriorityLoad()
        {
            Instances.CanvasCreated += OnCanvasCreated;
            return GH_LoadingInstruction.Proceed;
        }

        private static void OnCanvasCreated(GH_Canvas canvas)
        {
            if (Instances.DocumentEditor == null)
                return;

            if (_iconBox != null)
                return;

            var iconPath = Path.Combine(GH_Folders.AssemblyDirectory, "Resources", "mcp_icon.svg");
            Bitmap bitmap;
            if (File.Exists(iconPath))
            {
                var doc = SvgDocument.Open(iconPath);
                bitmap = doc.Draw(32, 32);
            }
            else
            {
                bitmap = new Bitmap(32, 32);
            }

            _iconBox = new PictureBox
            {
                Image = bitmap,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 32,
                Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.Transparent,
            };

            _bubbleLabel = new Label
            {
                AutoSize = true,
                Visible = false,
                BackColor = Color.FromArgb(255, 255, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            PositionControls();
            Instances.DocumentEditor.Controls.Add(_iconBox);
            Instances.DocumentEditor.Controls.Add(_bubbleLabel);
            Instances.DocumentEditor.Resize += (_, __) => PositionControls();
        }

        private static void PositionControls()
        {
            if (Instances.DocumentEditor == null || _iconBox == null || _bubbleLabel == null)
                return;

            var editor = Instances.DocumentEditor;
            _iconBox.Location = new Point(editor.ClientSize.Width - _iconBox.Width - 10,
                editor.ClientSize.Height - _iconBox.Height - 10);

            _bubbleLabel.Location = new Point(
                _iconBox.Left - _bubbleLabel.Width - 5,
                _iconBox.Top + (_iconBox.Height - _bubbleLabel.Height) / 2);
        }

        /// <summary>
        /// Displays a short message next to the MCP icon.
        /// </summary>
        public static void ShowMessage(string message, int durationMs = 3000)
        {
            if (_bubbleLabel == null)
                return;

            _bubbleLabel.Text = message;
            _bubbleLabel.Visible = true;
            PositionControls();
            var timer = new Timer { Interval = durationMs };
            timer.Tick += (s, e) =>
            {
                _bubbleLabel.Visible = false;
                timer.Dispose();
            };
            timer.Start();
        }
    }
}

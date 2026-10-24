using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EveCompanion;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MakeClickThrough();
        Loaded += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 24;
            Top = workArea.Top + 24;
        };
    }

    public void Update(IskSession? session)
    {
        SessionStateText.Text = session is null ? "IDLE" : "LIVE";
        RateText.Text = session?.RateText ?? "0 ISK/ч";
        DetailsText.Text = session is null
            ? "Сессия не запущена"
            : $"{session.TotalText}  ·  {session.DurationText}";
    }

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newStyle);
}

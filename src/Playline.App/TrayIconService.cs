using System.Drawing;
using System.IO;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Playline.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Icon _icon;
    private bool _disposed;

    public TrayIconService(
        Dispatcher dispatcher,
        Action show,
        Action discover,
        Action settings,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(discover);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(exit);

        _dispatcher = dispatcher;
        _icon = CreateIcon();
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(CreateMenuItem("Mostrar Playline", show));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(CreateMenuItem("Procurar jogos", discover));
        _menu.Items.Add(CreateMenuItem("Configurações", settings));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(CreateMenuItem("Sair", exit));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Playline",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                Invoke(show);
            }
        };
        _notifyIcon.MouseDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                Invoke(show);
            }
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private Forms.ToolStripMenuItem CreateMenuItem(string text, Action action)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += (_, _) => Invoke(action);
        return item;
    }

    private void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = _dispatcher.BeginInvoke(action);
    }

    private static Icon CreateIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            try
            {
                var associatedIcon = Icon.ExtractAssociatedIcon(processPath);
                if (associatedIcon is not null)
                {
                    return associatedIcon;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or IOException)
            {
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}

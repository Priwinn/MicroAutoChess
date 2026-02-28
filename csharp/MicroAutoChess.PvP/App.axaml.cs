using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MicroAutoChess.Core;
using System.Collections.Generic;

namespace MicroAutoChess.PvPApp
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Default configuration: 1 human + 1 AI
                var playerConfigs = new List<(int playerId, GamePlayerType type)>
                {
                    (1, GamePlayerType.Human),
                    (2, GamePlayerType.AI),
                    (3, GamePlayerType.AI),
                    (4, GamePlayerType.AI),
                };

                var orchestrator = new PvPOrchestrator(playerConfigs, masterSeed: 123);
                orchestrator.Start(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

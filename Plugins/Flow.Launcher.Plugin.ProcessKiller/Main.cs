using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.InteropServices;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    public class Main : IPlugin, IPluginI18n, IContextMenu
    {
        private ProcessHelper processHelper = new ProcessHelper();

        private static PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var termToSearch = query.Terms.Length <= 1
                ? null
                : string.Join(Plugin.Query.TermSeperater, query.Terms.Skip(1)).ToLower();

            var processlist = processHelper.GetMatchingProcesses(termToSearch);

            return !processlist.Any()
                ? null
                : CreateResultsFromProcesses(processlist, termToSearch);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_processkiller_plugin_description");
        }

        public List<Result> LoadContextMenus(Result result)
        {
            var menuOptions = new List<Result>();
            var processPath = result.SubTitle;

            // get all non-system processes whose file path matches that of the given result (processPath)
            var similarProcesses = processHelper.GetSimilarProcesses(processPath);

            menuOptions.Add(new Result
            {
                Title = _context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_instances"),
                SubTitle = processPath,
                Action = _ =>
                {
                    foreach (var p in similarProcesses)
                    {
                        processHelper.TryKill(p);
                    }

                    return true;
                },
                IcoPath = processPath
            });

            return menuOptions;
        }

        private List<Result> CreateResultsFromProcesses(List<ProcessResult> processlist, string termToSearch)
        {
            var results = new List<Result>();

            foreach (var pr in processlist)
            {
                var p = pr.Process;
                var path = processHelper.TryGetProcessFilename(p);
                results.Add(new Result()
                {
                    IcoPath = path,
                    Title = p.ProcessName + " - " + p.Id,
                    SubTitle = path,
                    TitleHighlightData = StringMatcher.FuzzySearch(termToSearch, p.ProcessName).MatchData,
                    Score = pr.Score,
                    Action = (c) =>
                    {
                        processHelper.TryKill(p);
                        return true;
                    }
                });
            }

            // When there are multiple results AND all of them are instances of the same executable
            // add a quick option to kill them all at the top of the results.
            var firstResult = results.FirstOrDefault()?.SubTitle;
            if (processlist.Count > 1 && !string.IsNullOrEmpty(termToSearch) && results.All(r => r.SubTitle == firstResult))
            {
                results.Insert(0, new Result()
                {
                    IcoPath = "Images/app.png",
                    Title = string.Format(_context.API.GetTranslation("flowlauncher_plugin_processkiller_kill_all"), termToSearch),
                    SubTitle = "",
                    Score = 200,
                    Action = (c) =>
                    {
                        foreach (var p in processlist)
                        {
                            processHelper.TryKill(p.Process);
                        }

                        return true;
                    }
                });
            }

            return results;
        }
    }
}

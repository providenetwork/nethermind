﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Nethermind.Core;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.DataModel;

namespace Nethermind.Runner.Runners
{
    public class JsonRpcRunner : IJsonRpcRunner
    {
        private readonly ILogger _logger;
        private readonly IConfigurationProvider _configurationProvider;
        private IWebHost _webHost;

        public JsonRpcRunner(IConfigurationProvider configurationProvider, ILogger logger)
        {
            _configurationProvider = configurationProvider;
            _logger = logger;
        }

        public void Start(InitParams initParams, IReadOnlyCollection<ModuleType> modules = null)
        {
            _logger.Info("Initializing JsonRPC");
            var host = $"http://{initParams.HttpHost}:{initParams.HttpPort}";
            _logger.Info($"Running server, url: {host}");

            var webHost = WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseUrls(host)
                .Build();

            if (modules != null && modules.Any())
            {
                _configurationProvider.EnabledModules = modules;
            }

            _logger.Info($"Starting http service, modules: {string.Join(", ", _configurationProvider.EnabledModules.Select(x => x))}");
            _webHost = webHost;
            _webHost.Start();
            _logger.Info("JsonRPC initialization completed");
        }

        public async Task StopAsync(IEnumerable<ModuleType> modules = null)
        {
            try
            {
                await _webHost.StopAsync();
                _logger.Info("Service stopped");
            }
            catch (Exception e)
            {
                _logger.Info($"Error during stopping service: {e}");
            }
        }
    }
}
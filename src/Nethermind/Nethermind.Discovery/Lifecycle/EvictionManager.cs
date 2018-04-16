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

using System.Collections.Concurrent;
using Nethermind.Core;
using Nethermind.Discovery.RoutingTable;

namespace Nethermind.Discovery.Lifecycle
{
    public class EvictionManager : IEvictionManager
    {
        private readonly ConcurrentDictionary<string, EvictionPair> _evictionPairs = new ConcurrentDictionary<string, EvictionPair>();
        private readonly INodeTable _nodeTable;
        private readonly ILogger _logger;

        public EvictionManager(INodeTable nodeTable, ILogger logger)
        {
            _nodeTable = nodeTable;
            _logger = logger;
        }

        public void StartEvictionProcess(INodeLifecycleManager evictionCandidate, INodeLifecycleManager replacementCandidate)
        {
            _logger.Info($"Starting eviction process, evictionCandidate: {evictionCandidate.ManagedNode}, replacementCandidate: {replacementCandidate.ManagedNode}");

            var newPair = new EvictionPair
            {
                EvictionCandidate = evictionCandidate,
                ReplacementCandidate = replacementCandidate,
            };

            var pair = _evictionPairs.GetOrAdd(evictionCandidate.ManagedNode.IdHashText, newPair);
            if (pair != newPair)
            {
                //existing eviction in process
                //TODO add queue for further evictions
                _logger.Info($"Existing eviction in process, evictionCandidate: {evictionCandidate.ManagedNode}, replacementCandidate: {replacementCandidate.ManagedNode}");
                return;
            }
           
            evictionCandidate.OnStateChanged += OnStateChange;
            evictionCandidate.StartEvictionProcess();
        }

        private void OnStateChange(object sender, NodeLifecycleState state)
        {
            if (!(sender is INodeLifecycleManager evictionCandidate))
            {
                return;
            }

            if (!_evictionPairs.TryGetValue(evictionCandidate.ManagedNode.IdHashText, out EvictionPair evictionPair))
            {
                return;
            }

            if (state == NodeLifecycleState.Active)
            {
                //survived eviction
                _logger.Info($"Survived eviction process, evictionCandidate: {evictionCandidate.ManagedNode}, replacementCandidate: {evictionPair.ReplacementCandidate.ManagedNode}");
                evictionPair.ReplacementCandidate.LostEvictionProcess();
                CloseEvictionProcess(evictionCandidate);
            }
            else if (state == NodeLifecycleState.Unreachable)
            {
                //lost eviction, being replaced in nodeTable
                _nodeTable.ReplaceNode(evictionCandidate.ManagedNode, evictionPair.ReplacementCandidate.ManagedNode);
                _logger.Info($"Lost eviction process, evictionCandidate: {evictionCandidate.ManagedNode}, replacementCandidate: {evictionPair.ReplacementCandidate.ManagedNode}");
                CloseEvictionProcess(evictionCandidate);
            }
        }

        private void CloseEvictionProcess(INodeLifecycleManager evictionCandidate)
        {
            _evictionPairs.TryRemove(evictionCandidate.ManagedNode.IdHashText, out var _);
            evictionCandidate.OnStateChanged -= OnStateChange;
        }
    }
}
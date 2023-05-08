﻿using ApplicationLogic.Extensions;
using ApplicationLogic.Interfaces;
using ApplicationLogic.Interfaces.Dao;
using ApplicationLogic.Services;
using Entities.DomainInterfaces.Live;
using Microsoft.Extensions.Logging;

namespace ApplicationLogic.Usecases
{
    public class Monitor
    {
        private readonly IPodReadService _readServiceService;

        private readonly ILogger<Monitor> _logger;

        private readonly ResourceManagementService _resourceManagementService;

        private readonly IEnvironmentClient _client;

        public Monitor(IEnvironmentClient client, IPodReadService readServiceService, IPodWriteService writeServiceService, ILogger<Monitor> logger)
        {
            _readServiceService = readServiceService;
            _logger = logger;
            _client = client;
            _resourceManagementService = new ResourceManagementService(client, readServiceService, writeServiceService);
        }

        public async Task GarbageCollection()
        {
            await _client.GarbageCollection();
        }

        public async Task MatchInstanceDemandOnPods()
        {
            var podTypes = _readServiceService.GetAllPodTypes();

            foreach (var podType in podTypes)
            {
                var podCount = _readServiceService.GetPodInstances(podType.Type)?.Count;

                if (podCount < podType.MinimumNumberOfInstances)
                {
                    await _resourceManagementService.IncreaseNumberOfPodInstances(podType.Type).ConfigureAwait(false);

                    _logger.LogCritical("matching pod demand...");
                }
            }
        }

        public async Task RemoveOrReplaceDeadPodsFromModel()
        {
            await Try.WithConsequenceAsync(async () =>
            {
                var runningContainerIds = (await _client.GetRunningServices())?.ToHashSet();

                if (runningContainerIds is null) return Task.CompletedTask;

                var allPods = _readServiceService.GetAllPods();

                foreach (var podInstance in allPods)
                {
                    var minNumberOfInstances = _readServiceService.GetPodType(podInstance.Type)!.MinimumNumberOfInstances;

                    if (IsAnyPodServiceDown(podInstance, runningContainerIds))
                    {
                        if (IsNumberOfRunningInstancesGreaterThanRequired(podInstance, minNumberOfInstances))
                        {
                            await _resourceManagementService.RemovePodInstance(podInstance).ConfigureAwait(false);

                            _logger.LogCritical("i killed a pod because we had too many already");
                        }
                        else
                        {
                            await _resourceManagementService.ReplacePodInstance(podInstance).ConfigureAwait(false);

                            _logger.LogCritical("i replaced a pod because we think it was dead");
                        }
                    }
                }

                return Task.CompletedTask;
            }, retryCount: 2).ConfigureAwait(false);

            bool IsAnyPodServiceDown(IPodInstance podInstance, HashSet<string> runningContainerIds)
            {
                return podInstance.ServiceInstances.Any(serviceInstance => runningContainerIds.Contains(serviceInstance.ContainerInfo?.Id ?? string.Empty)) is false;
            }

            bool IsNumberOfRunningInstancesGreaterThanRequired(IPodInstance podInstance, int minNumberOfInstances)
            {
                var count = _readServiceService.GetServiceInstances(podInstance.Type)!.Count;

                return count > minNumberOfInstances;
            }
        }
    }
}

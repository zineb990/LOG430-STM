﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Ambassador.Dto;
using ApplicationLogic.Interfaces;
using ApplicationLogic.Services;
using Entities;
using Entities.BusinessObjects;
using Entities.BusinessObjects.Live;
using Entities.BusinessObjects.Planned;
using Entities.BusinessObjects.States;
using Entities.DomainInterfaces.Live;
using Entities.DomainInterfaces.Planned;
using Entities.DomainInterfaces.ResourceManagement;

namespace ApplicationLogic.Usecases
{
    public class SubscriptionUC
    {
        private readonly IRepositoryWrite _repositoryWrite;
        
        private readonly IRepositoryRead _repositoryRead;

        private readonly IEnvironmentClient _environmentClient;

        private readonly IScheduler _scheduler;

        private static string NodeAddress => Environment.GetEnvironmentVariable("SERVICES_ADDRESS")!;

        public SubscriptionUC(IRepositoryWrite repositoryWrite, IRepositoryRead repositoryRead, IEnvironmentClient environmentClient, IScheduler scheduler)
        {
            _repositoryWrite = repositoryWrite;
            _repositoryRead = repositoryRead;
            _environmentClient = environmentClient;
            _scheduler = scheduler;

            _scheduler.TryAddTask(DiscoverServices);
        }

        private async Task DiscoverServices()
        {
            var unregisteredServices = await GetUnregisteredServices();

            foreach (var unregisteredService in unregisteredServices!)
            {
                var service = await _environmentClient.GetContainerInfo(unregisteredService);

                var newService = CreateService(service);

                CreateServiceType(service);

                CreateOrUpdatePodInstance(service, newService);
            }

            async Task<List<string>?> GetUnregisteredServices()
            {
                var runningServicesIds = await _environmentClient.GetRunningServices();

                var registeredServices = _repositoryRead.GetAllServices().ToDictionary(s => s.Id);

                var unregisteredServices = runningServicesIds?.Where(runningService => registeredServices.ContainsKey(runningService) is false).ToList();
                
                return unregisteredServices;
            }

            ServiceInstance CreateService((ContainerInfo CuratedInfo, IContainerConfig RawConfig) service)
            {
                var newService = new ServiceInstance()
                {
                    Id = service.RawConfig.Config.Config.Env.First(e => e.ToString().StartsWith("ID=")),
                    ContainerInfo = service.CuratedInfo,
                    Address = NodeAddress,
                    Type = GetServiceTypeName(service.CuratedInfo.Labels),
                    PodId = GetPodId(service.CuratedInfo.Labels)
                };

                newService.ServiceStatus = new ReadyState(newService);

                return newService;
            }

            void CreateServiceType((ContainerInfo CuratedInfo, IContainerConfig RawConfig) service)
            {
                var curatedInfoLabels = service.CuratedInfo.Labels;

                var podType = GetPodName(curatedInfoLabels);

                var serviceType = new ServiceType()
                {
                    ContainerConfig = service.RawConfig,
                    Type = GetServiceTypeName(curatedInfoLabels),
                    ComponentCategory = GetComponentCategory(curatedInfoLabels),
                    IsPodSidecar = GetIsSidecar(curatedInfoLabels),
                    PodName = podType
                };

                UpdateOrCreatePodType(podType, service, serviceType);
            }

            void UpdateOrCreatePodType(string podType, (ContainerInfo CuratedInfo, IContainerConfig RawConfig) service, IServiceType serviceType)
            {
                var pod = _repositoryRead.GetPodType(podType);

                if (pod is null)
                {
                    _repositoryWrite.AddOrUpdatePodType(new PodType()
                    {
                        Type = podType,
                        MinimumNumberOfInstances = GetMinimumNumberOfInstances(service.CuratedInfo.Labels),
                        Sidecar = GetIsSidecar(service.CuratedInfo.Labels) ? serviceType : pod?.Sidecar ?? default,
                        ServiceTypes = pod?.ServiceTypes.Add(serviceType) ?? ImmutableList<IServiceType>.Empty.Add(serviceType),
                    });
                }
            }

            void CreateOrUpdatePodInstance((ContainerInfo CuratedInfo, IContainerConfig RawConfig) service, IServiceInstance newService)
            {
                var podId = GetPodId(service.CuratedInfo.Labels);

                var pod = _repositoryRead.GetPodById(podId);

                pod ??= new PodInstance()
                {
                    ServiceInstances = pod?.ServiceInstances?.Add(newService) ??
                                       ImmutableList<IServiceInstance>.Empty.Add(newService),
                    Type = GetPodName(service.CuratedInfo.Labels),
                    Id = podId
                };

                _repositoryWrite.AddOrUpdatePod(pod);
            }
        }

        private string GetLabelValue(ServiceLabelsEnum serviceLabels, ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            labels.TryGetValue(serviceLabels, out var label);

            return label ?? string.Empty;
        }

        private string GetServiceTypeName(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            return GetLabelValue(ServiceLabelsEnum.SERVICE_TYPE_NAME, labels);
        }

        private string GetComponentCategory(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            var value = GetLabelValue(ServiceLabelsEnum.COMPONENT_CATEGORY, labels);

            return string.IsNullOrEmpty(value) ? nameof(ServiceCategoriesEnum.Undefined) : value;
        }

        private int GetMinimumNumberOfInstances(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            uint.TryParse(GetLabelValue(ServiceLabelsEnum.MINIMUM_NUMBER_OF_INSTANCES, labels), out var nbInstances);
            
            return Convert.ToInt32(nbInstances);
        }

        private string GetPodName(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            return GetLabelValue(ServiceLabelsEnum.POD_NAME, labels);
        }

        private string GetPodId(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            return GetLabelValue(ServiceLabelsEnum.POD_ID, labels);
        }

        private bool GetIsSidecar(ConcurrentDictionary<ServiceLabelsEnum, string> labels)
        {
            bool.TryParse(GetLabelValue(ServiceLabelsEnum.IS_POD_SIDECAR, labels), out var isSidecar);

            return isSidecar;
        }
    }
}

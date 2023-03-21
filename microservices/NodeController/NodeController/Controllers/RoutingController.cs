﻿using Ambassador;
using Ambassador.BusinessObjects;
using ApplicationLogic.Extensions;
using ApplicationLogic.Usecases;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NodeController.External.Docker;
using NodeController.External.Repository;

namespace NodeController.Controllers
{
    [EnableCors("AllowOrigin")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class RoutingController : ControllerBase
    {
        private readonly RoutingUC _routingUc;

        private readonly HeadersUC _headersUc = new();

        private readonly ILogger<RoutingController> _logger;

        public RoutingController(ILogger<RoutingController> logger)
        {
            _logger = logger;

            var readModel = new PodReadModel(HostInfo.ServiceAddress);

            _routingUc = new(readModel);
        }

        [HttpGet]
        [ActionName(nameof(RouteByServiceType))]
        public ActionResult<IEnumerable<RoutingData>> RouteByServiceType(string caller, string serviceType, LoadBalancingMode mode)
        {
            return Ok(Try.WithConsequenceAsync(() =>
            {
                var routingDatas = _routingUc.RouteByDestinationType(caller, serviceType, mode).ToList();

                foreach (var routingData in routingDatas)
                {
                    _headersUc.AddJsonHeader(routingData);

                    _headersUc.AddAuthorizationHeaders(routingData, serviceType);

                    _logger.LogInformation($"routing service to {routingData.Address}");
                }

                return Task.FromResult(routingDatas);
            }, retryCount: 2));
        }
    }
}

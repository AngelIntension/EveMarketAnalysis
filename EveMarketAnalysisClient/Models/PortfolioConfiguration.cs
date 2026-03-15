namespace EveMarketAnalysisClient.Models;

public record PortfolioConfiguration(
    long ProcurementStationId = 60003760,
    int ProcurementRegionId = 10000002,
    long SellingHubStationId = 60003760,
    int SellingHubRegionId = 10000002,
    int ManufacturingSystemId = 30000142,
    decimal BuyingBrokerFeePercent = 3.0m,
    decimal SellingBrokerFeePercent = 3.0m,
    decimal SalesTaxPercent = 3.6m,
    decimal MinIskPerHour = 25_000_000m,
    decimal DailyIncomeGoal = 750_000_000m,
    int ManufacturingSlots = 11,
    int? WhatIfME = null,
    int? WhatIfTE = null);

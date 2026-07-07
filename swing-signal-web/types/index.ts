export interface MacroIndicatorValueDto {
  value: number;
  signal: string;
  trend: string;
}

export interface MacroRegimeDto {
  indicators: Record<string, MacroIndicatorValueDto>;
  asOf: string;
}

export interface HistoricalMatchDto {
  date: string;
  similarityScore: number;
  topPercent: number;
  indicatorValues: Record<string, number>;
}

export interface OddsForPeriodDto {
  totalCases: number;
  positiveCases: number;
  positiveOdds: number;
  averageReturn: number;
  medianReturn: number;
  bestCase: number;
  worstCase: number;
  priceTargetLow: number | null;
  priceTargetMid: number | null;
  priceTargetHigh: number | null;
  baseRate: number | null;
  edge: number;
}

export interface AnalogPointDto {
  date: string;
  aboveMa200: boolean | null;
}

export interface AnalogBreakdownDto {
  currentAboveMa200: boolean | null;
  aboveCount: number;
  aboveOdds3M: number | null;
  aboveMedian3M: number | null;
  belowCount: number;
  belowOdds3M: number | null;
  belowMedian3M: number | null;
  points: AnalogPointDto[];
}

export interface AssetOddsDto {
  symbol: string;
  name: string;
  matchesUsed: number;
  currentPrice: number | null;
  oneMonth: OddsForPeriodDto;
  threeMonths: OddsForPeriodDto;
  sixMonths: OddsForPeriodDto;
  explanations: string[];
  disclaimer: string;
  breakdown: AnalogBreakdownDto | null;
}

export interface AssetPeriodOddsDto {
  symbol: string;
  name: string;
  days: number;
  matchesUsed: number;
  currentPrice: number | null;
  odds: OddsForPeriodDto;
  disclaimer: string;
}

export interface BacktestBucketDto {
  predictedRange: string;
  predictions: number;
  avgPredictedOdds: number;
  actualPositiveRate: number;
}

export interface BacktestResultDto {
  symbol: string;
  name: string;
  days: number;
  topK: number;
  totalPredictions: number;
  firstPrediction: string;
  lastPrediction: string;
  directionalAccuracy: number;
  brierScore: number;
  avgPredictedOdds: number;
  actualPositiveRate: number;
  calibration: BacktestBucketDto[];
  interpretation: string;
  disclaimer: string;
}

export interface BacktestComparisonDto {
  baseline: BacktestResultDto;
  current: BacktestResultDto;
  summary: string;
}

export interface CandleDto {
  openTime: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface UserProfileDto {
  email: string;
  plan: string;
  isEmailConfirmed: boolean;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  email: string;
  plan: string;
}

export interface SymbolSearchResultDto {
  symbol: string;
  name: string;
  type: string;
  exchange: string;
}

export interface PopularAssetDto {
  symbol: string;
  name: string;
  currentPrice: number;
  changePct: number;
  spark: number[];
  odds3M: number | null;
  baseRate3M: number | null;
  edge3M: number | null;
}

export interface AssetDto {
  id: string;
  symbol: string;
  name: string;
  marketType: string;
  isActive: boolean;
}

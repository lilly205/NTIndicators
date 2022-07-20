//
// Copyright (C) 2022, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX.DirectWrite;
using SharpDX;
using SharpDX.Direct2D1;
using Point = System.Windows.Point;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	public class Trades{ //Structure for active trades
		public int barNumber=-1; // Record bar number where this trade occurred 
		public double target=-1; // Profit target to reach
		public double loss=-1; // Stoploss
		public int tradeDirection = -1; // Long/Short
		
		public Trades(int barNumber, double target, double loss, int tradeDirection) // Default constructor 
		{
			this.barNumber=barNumber;
			this.target=target;
			this.loss=loss;
			this.tradeDirection=tradeDirection;
		}
		public void AdjustValues(int barNumber, double target, double loss, int tradeDirection) // Used when a trade doesnt have a stoploss temporarily
		{
			this.barNumber=barNumber;
			this.target=target;
			this.loss=loss;
			this.tradeDirection=tradeDirection;
		}
	}
	
	//should probably change all checks to be if the high/low of current bar meets target instead of current price. Just saw where the bar managed to tick lower and insta reverse so it didnt get counted as a loss
	//shorts seem to have loss one tick too high?
	
	public class MyCustomIndicator : Indicator
	{
		protected TimeSpan morningOpen = new TimeSpan(9,30,0); //Time for market session open
		protected TimeSpan sessionClose = new TimeSpan(15,30,0); //Time for market session close
		protected double lastLongExtreme = -1.0; //Last extreme in an uptrend
		protected double lastShortExtreme = -1.0; //Last extreme in a downtrend
		protected int directionChangeBar = -1; // This is the bar where a directional change occurred. Resets to -1 when uptrend/downtrend continues
		protected int lastBarProcessed = -1; // Last bar processed number
		protected int lastDirection = 0; //0 is no direction (only used for initialization), 1 is long, 2 is short
		protected List<Trades> tradesList = new List<Trades>(); //Keeps track of the active trades
		protected int totalTrades = 0; // Total number of trades
		protected int successfulTrades = 0; // Total number of successful trades
		protected int shortWins = 0; // Total number of short trades that are successful
		protected int shortTrades = 0; // Total number of short trades
		protected int longWins = 0; // Total number of long trades that are successful
		protected int longTrades = 0; // Total number of short trades
		protected double profitTarget = 1.0; // Profit target (always adds .25 to ensure successful trade)
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description			= @"Calculates the chance that a random trade (given candle break direction change) will succeed given a target profit";
				Name				= "My Custom Indicator";
				//Calculate			= Calculate.OnEachTick;
				Calculate			= Calculate.OnPriceChange;
				CountDown			= true;
				DisplayInDataBox	= false;
				DrawOnPricePanel	= false;
				IsChartOnly			= true;
				IsOverlay			= true;
				ShowPercent			= false;
				profitTarget += 0.25;
			}
		}

		protected override void OnBarUpdate()
		{
			if (lastBarProcessed == -1)
			{
				lastBarProcessed = CurrentBar-1;
			}
			if (lastBarProcessed != CurrentBar-1)
			{
				//check if any values in tradesList need filled out
				for (int i = tradesList.Count - 1; i >= 0; i--)
				{
				    if (tradesList[i].loss == -1)
					{
						if (tradesList[i].tradeDirection == 1)
						{
							tradesList[i].loss = Bars.GetLow(CurrentBar-1) - 0.25;
						}
						else
						{
							tradesList[i].loss = Bars.GetHigh(CurrentBar-1) + 0.25;
						}
					}
				}
				lastLongExtreme=Bars.GetHigh(CurrentBar-1);
				lastShortExtreme=Bars.GetLow(CurrentBar-1);
				
				lastBarProcessed = CurrentBar-1;	
				directionChangeBar = -1;
				//check for any missing values and use the extremes for them if no value
			}
			if (CurrentBar>0){
				ProcessBar();
			//NinjaTrader.Gui.Tools.SimpleFont displayFont = new NinjaTrader.Gui.Tools.SimpleFont("Courier New", 12) { Size = 20, Bold = true };
	   			string displayText = "";
				if (directionChangeBar > -1 && directionChangeBar != CurrentBar-1)
				{
					Print("Reset direction change bar" + directionChangeBar.ToString());
					directionChangeBar = -1;	
				}
				for (int i = tradesList.Count - 1; i >= 0; i--)
				{
				    // if trade was long
					if (tradesList[i].loss != -1)
					{
						if (tradesList[i].tradeDirection == 1)
						{
							if (tradesList[i].target<=Bars.GetHigh(CurrentBar)) //price is higher than target
							{
								Print("RECORDED INFO FOR BAR 1(win): " + tradesList[i].target.ToString() + ", " + tradesList[i].loss.ToString() + ", " + tradesList[i].tradeDirection.ToString() + "| Current price: " + Bars.LastPrice.ToString());
								totalTrades+=1;
								successfulTrades+=1;
								longWins+=1;
								longTrades+=1;
								tradesList.RemoveAt(i);
							}
							else if (tradesList[i].loss>Bars.GetLow(CurrentBar)) // price is lower than loss
							{
								Print("RECORDED INFO FOR BAR 2(loss): " + tradesList[i].target.ToString() + ", " + tradesList[i].loss.ToString() + ", " + tradesList[i].tradeDirection.ToString() + "| Current price: " + Bars.LastPrice.ToString());
								totalTrades+=1;
								longTrades+=1;
								tradesList.RemoveAt(i);
							}
						}
						else // if trade was short
						{
							if (tradesList[i].target>=Bars.GetLow(CurrentBar)) // price is lower than target
							{
								Print("RECORDED INFO FOR BAR 3(win): " + tradesList[i].target.ToString() + ", " + tradesList[i].loss.ToString() + ", " + tradesList[i].tradeDirection.ToString() + "| Current price: " + Bars.LastPrice.ToString());
								totalTrades+=1;
								successfulTrades+=1;
								shortWins+=1;
								shortTrades+=1;
								tradesList.RemoveAt(i);
							}
							else if (Bars.GetHigh(CurrentBar)>tradesList[i].loss) // price is higher than loss
							{
								Print("RECORDED INFO FOR BAR 4(loss): " + tradesList[i].target.ToString() + ", " + tradesList[i].loss.ToString() + ", " + tradesList[i].tradeDirection.ToString() + "| Current price: " + Bars.LastPrice.ToString());
								totalTrades+=1;
								shortTrades+=1;
								tradesList.RemoveAt(i);
							}
						}
					}
				}
				// Display text 
	  		 	displayText +=  "Successful: " + successfulTrades.ToString() + " | Total: " + totalTrades.ToString() + " " + (((double)successfulTrades/(double)totalTrades)*100).ToString() + "%" +" ||| Long Successful: " + longWins.ToString() + " | Total: " + longTrades.ToString() + " " + (((double)longWins/(double)longTrades)*100).ToString() + "%" + " ||| Short Successful: " + shortWins.ToString() + " | Total: " + shortTrades.ToString() + " " + (((double)shortWins/(double)shortTrades)*100).ToString() +"%";
				
	
	   			Draw.TextFixed(this,"riskText",displayText,TextPosition.TopRight, ChartControl.Properties.ChartText, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);}
	
		}
		
		protected void ProcessBar()
		{
			//determine direction if no direction has been set
			if (lastDirection == 0)
			{
				if (Bars.LastPrice > Bars.GetHigh(CurrentBar-1))
				{
					//log data into list
					lastDirection=1;
					directionChangeBar = CurrentBar;
				}
				else if (Bars.LastPrice < Bars.GetLow(CurrentBar-1))
				{
					//log data into list
					lastDirection=2;
					directionChangeBar = CurrentBar;
				}
			}
			
			//determine directional extreme value
			if (lastDirection == 1)
			{
				if (Bars.LastPrice>Bars.GetHigh(CurrentBar-1))
				{
					if (lastLongExtreme!=-1.0)
					{
						if (Bars.LastPrice > lastLongExtreme)
						{
							lastLongExtreme = Bars.LastPrice;
						}
					}
					else
					{
						lastLongExtreme = Bars.LastPrice;	
					}
				}
			}
			else if (lastDirection == 2)
			{
				if (Bars.LastPrice<Bars.GetLow(CurrentBar-1))
				{
					if (lastShortExtreme!=-1.0)
					{
						if (Bars.LastPrice < lastShortExtreme)
						{
							lastShortExtreme = Bars.LastPrice;	
						}
					}
					else
					{
							lastShortExtreme = Bars.LastPrice;	
					}
				}
			}
			
			//change directions
			if (lastDirection == 1)
			{
				if (Bars.LastPrice < Bars.GetLow(CurrentBar-1) && directionChangeBar== -1)
				{
					Print("Long-Short: First time changing directions for current bar");
					//log data into list
					lastDirection=2;
					if (lastLongExtreme > Bars.GetHigh(CurrentBar - 1))
					{
						tradesList.Add(new Trades(CurrentBar, Bars.GetLow(CurrentBar-1) - profitTarget , -1.0, lastDirection));
					}
					else
					{
						tradesList.Add(new Trades(CurrentBar, Bars.GetLow(CurrentBar-1) - profitTarget , Bars.GetHigh(CurrentBar-1)+0.25, lastDirection));
					}
					lastShortExtreme = Bars.LastPrice;	
					directionChangeBar = CurrentBar;
				}
				else if (Bars.LastPrice < Bars.GetLow(CurrentBar-1) && directionChangeBar!= -1)
				{
					if (lastLongExtreme<=Bars.GetHigh(CurrentBar-1))
					{
						//bar is not as high as previous bar, allowed to switch
						Print("Long-Short: Current bar did not break higher before going lower, allowed to switch");
						lastDirection=2;
						if (lastLongExtreme > Bars.GetHigh(CurrentBar - 1))
						{
							tradesList.Add(new Trades(CurrentBar, Bars.GetLow(CurrentBar-1) - profitTarget , -1, lastDirection));
						}
						else
						{
							tradesList.Add(new Trades(CurrentBar, Bars.GetLow(CurrentBar-1) - profitTarget , Bars.GetHigh(CurrentBar-1)+0.25, lastDirection));
						}
						lastShortExtreme = Bars.LastPrice;	
						directionChangeBar = CurrentBar;
					}
					else {
						Print("Long-Short: Current bar broke higher before going lower, not allowed to switch");
					}
				}
			}
			else if (lastDirection==2)
			{
				if (Bars.LastPrice > Bars.GetHigh(CurrentBar-1) && directionChangeBar== -1)
				{
					Print("Short-Long: First time changing directions for current bar");
					//log data into list
					lastDirection=1;
					if (lastShortExtreme < Bars.GetLow(CurrentBar - 1))
					{
						tradesList.Add(new Trades(CurrentBar, Bars.GetHigh(CurrentBar-1) + profitTarget , -1, lastDirection));
					}
					else
					{
						tradesList.Add(new Trades(CurrentBar, Bars.GetHigh(CurrentBar-1) + profitTarget , Bars.GetLow(CurrentBar-1)-0.25, lastDirection));
					}
					lastLongExtreme = Bars.LastPrice;
					directionChangeBar = CurrentBar;
				}
				else if (Bars.LastPrice > Bars.GetHigh(CurrentBar-1) && directionChangeBar!= -1)
				{
					if (lastShortExtreme<=Bars.GetLow(CurrentBar-1))
					{
						//bar is not as high as previous bar, allowed to switch
						Print("Short-Long: Current bar did not break lower before going higher, allowed to switch");
						lastDirection=1;
						if (lastShortExtreme < Bars.GetLow(CurrentBar - 1))
						{
							tradesList.Add(new Trades(CurrentBar, Bars.GetHigh(CurrentBar-1) + profitTarget , -1, lastDirection));
						}
						else
						{
							tradesList.Add(new Trades(CurrentBar, Bars.GetHigh(CurrentBar-1) + profitTarget , Bars.GetLow(CurrentBar-1)-0.25, lastDirection));
						}
						lastLongExtreme = Bars.LastPrice;	
						directionChangeBar = CurrentBar;
					}
					else {
						Print("Short-Long: Current bar broke lower before going higher, not allowed to switch");
					}
				}
			}
			
			
			//TimeSpan currentBarTime = Bars.LastBarTime.TimeOfDay;
			//DateTime currentBarDate = Bars.LastBarTime.Date;
			//if ((currentBarTime > morningOpen) && (currentBarTime < sessionClose))
			//{ 
				
				//count the bar and do stuff. Maybe make sure current bar is part of today?
			//}
			//else 
			//{
				//clear list and all stats
			//}
			return;
		}

		#region Properties
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "CountDown", Order = 1, GroupName = "NinjaScriptParameters")]
		public bool CountDown
		{ get; set; }

		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "ShowPercent", Order = 2, GroupName = "NinjaScriptParameters")]
		public bool ShowPercent
		{ get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private MyCustomIndicator[] cacheMyCustomIndicator;
		public MyCustomIndicator MyCustomIndicator(bool countDown, bool showPercent)
		{
			return MyCustomIndicator(Input, countDown, showPercent);
		}

		public MyCustomIndicator MyCustomIndicator(ISeries<double> input, bool countDown, bool showPercent)
		{
			if (cacheMyCustomIndicator != null)
				for (int idx = 0; idx < cacheMyCustomIndicator.Length; idx++)
					if (cacheMyCustomIndicator[idx] != null && cacheMyCustomIndicator[idx].CountDown == countDown && cacheMyCustomIndicator[idx].ShowPercent == showPercent && cacheMyCustomIndicator[idx].EqualsInput(input))
						return cacheMyCustomIndicator[idx];
			return CacheIndicator<MyCustomIndicator>(new MyCustomIndicator(){ CountDown = countDown, ShowPercent = showPercent }, input, ref cacheMyCustomIndicator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.MyCustomIndicator MyCustomIndicator(bool countDown, bool showPercent)
		{
			return indicator.MyCustomIndicator(Input, countDown, showPercent);
		}

		public Indicators.MyCustomIndicator MyCustomIndicator(ISeries<double> input , bool countDown, bool showPercent)
		{
			return indicator.MyCustomIndicator(input, countDown, showPercent);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.MyCustomIndicator MyCustomIndicator(bool countDown, bool showPercent)
		{
			return indicator.MyCustomIndicator(Input, countDown, showPercent);
		}

		public Indicators.MyCustomIndicator MyCustomIndicator(ISeries<double> input , bool countDown, bool showPercent)
		{
			return indicator.MyCustomIndicator(input, countDown, showPercent);
		}
	}
}

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QLearningOnPerishableInventory
{
    public class FreezingInventory
    {
        const int ProductLife = 8;
        const int MaxInventoryPosition = 41;
        const int RemainingLivesCount_S1 = MaxInventoryPosition * ProductLife;
        const int OrderQuantitiesCount1 = 9;
        const int a = 4;
        const int b = 5;
        
        const double InitialSalePricePerUnit = 5.0;

        // costs
        const double FreezerCostFixedPerDay = -InitialSalePricePerUnit;
        const double HoldingCostPerDay = -0.05;
        const int LotSize = 5;
        const double OutageCost = -InitialSalePricePerUnit, ShortageCost = -1;

        const double LearningRate = 0.01;
        const long Episodes = 2_000_000;
        const int MaxStepPerEpisode = MaxInventoryPosition;
        double Epsilon = 1;

        static readonly double EpsilonDecay = Episodes > 1_000_000L ? Math.Exp(Math.Log(0.1) / 1_000_000) : Math.Exp(Math.Log(0.1) / Episodes);
        const double FutureDiscount = 0.99;

        static Random randm = new Random(DateTime.UtcNow.Millisecond);
        QTable1 Table1;

        long PreviousIterCount = 0;
        readonly int[] OrderQuantities;

        public long LastIdx = 0;

        public FreezingInventory()
        {
            OrderQuantities = new int[OrderQuantitiesCount1];
        }

        public double[] Start(IProgress<double> pr, double eps = 1L)
        {
            double[] EpisodeRewards = new double[Episodes];

            Epsilon = eps;
            long ep = 0;
            var itr = MathFunctions.GetDemandByGammaDist(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();

            while (ep < Episodes)
            {
                double ep_reward = 0;
                var ProductsOnHand = new List<Product>(MaxInventoryPosition);

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    double HoldingCost = 0;
                    itr.MoveNext();
                    var actual_demand = itr.Current;

                    int life_rem = ProductsOnHand.Sum(p => ProductLife - p.LifeSpent);

                    // determine order quantity
                    int oq, lot;
                    int total_product_count = ProductsOnHand.Count;
                    int max_oq = MakeLot(MaxInventoryPosition - total_product_count);
                    var dec_rnd = randm.NextDouble();

                    if (dec_rnd < Epsilon) // explore
                    {
                        lot = OrderQuantities[randm.Next(max_oq)];
                    }
                    else // exploit
                    {
                        var key = Table1.GetMaxOrderQuantityForState(total_product_count, life_rem, max_oq);
                        lot = key.Action;
                    }
                    oq = lot * LotSize;

                    // recieve the arrived products that was ordered previously
                    for (int i = oq - 1; i >= 0; i--)
                    {
                        ProductsOnHand.Add(new Product(0));
                    }

                    // calculate shortage
                    int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);

                    double sale_price_for_step = 0.0;
                    // remove the products consumed by customers
                    if (ProductsOnHand.Count > 0)
                    {
                        if (actual_demand >= ProductsOnHand.Count)
                        {
                            // dynamic pricing
                            sale_price_for_step = ProductsOnHand
                                .Sum(p =>
                                {
                                    double curLife = (double)p.LifeSpent / ProductLife;
                                    if (curLife <= 0.4)
                                        return InitialSalePricePerUnit;
                                    var discount = InitialSalePricePerUnit * curLife / 2;
                                    return InitialSalePricePerUnit - discount;
                                });

                            ProductsOnHand.Clear();
                        }
                        else
                        {
                            // dynamic pricing
                            sale_price_for_step = ProductsOnHand
                                .Take(actual_demand)
                                .Sum(p =>
                                {
                                    double curLife = (double)p.LifeSpent / ProductLife;
                                    if (curLife <= 0.4)
                                        return InitialSalePricePerUnit;
                                    var discount = InitialSalePricePerUnit * curLife / 2;
                                    return InitialSalePricePerUnit - discount;
                                });

                            // LIFO policy : removing the newest products
                            ProductsOnHand.RemoveRange(ProductsOnHand.Count - actual_demand, actual_demand);
                        }
                    }

                    // discard outdated product and calculate outage amount
                    int To = 0;
                    var new_life_rem = 0;
                    for (int i = ProductsOnHand.Count - 1; i >= 0; i--)
                    {
                        ProductsOnHand[i].LifeSpent++;
                        if (ProductsOnHand[i].LifeSpent > ProductLife)
                        {
                            To++;
                            ProductsOnHand.RemoveAt(i);
                        }
                        else
                        {
                            HoldingCost += HoldingCostPerDay;
                            new_life_rem += ProductLife - ProductsOnHand[i].LifeSpent;
                        }
                    }

                    double reward = Ts * ShortageCost + 
                                    To * OutageCost + 
                                    HoldingCost +
                                    sale_price_for_step + 
                                    FreezerCostFixedPerDay;
                    System.Diagnostics.Debug.WriteLine(HoldingCost);
                    ep_reward += reward;

                    // calculate max q(s',a')
                    // quantity in next round = on hand inventory + orders in transit - next day demand
                    var new_total_product_count = ProductsOnHand.Count;
                    var max_oq_tmp = MakeLot(OrderQuantities.Last() * 5 - new_total_product_count);
                    var next_maxq_key = Table1.GetMaxOrderQuantityForState(new_total_product_count, new_life_rem, max_oq_tmp);
                    var max_q_future = Table1[next_maxq_key];

                    // update q table
                    var state = new QuantityLifeState(total_product_count, life_rem);
                    var sa_pair = new QTableKey1(state, lot);
                    Table1[sa_pair] = (1 - LearningRate) * Table1[sa_pair] + LearningRate * (reward + FutureDiscount * max_q_future);
                }

                EpisodeRewards[ep] = ep_reward;

                if(Epsilon > 0.05)
                    Epsilon *= EpsilonDecay;
                //LearningRate *= EpsilonDecay;
                if (ep % 5000 == 0)
                    pr.Report(ep * 100.0 / Episodes);
                ep++;
            }

            return EpisodeRewards;
        }

        public async Task PrepareTable()
        {
            var InventoryPositions = new int[MaxInventoryPosition];
            var RemainingLives = new int[RemainingLivesCount_S1];

            await Task.WhenAll(
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < OrderQuantitiesCount1; i++)
                    {
                        OrderQuantities[i] = i;
                    }
                }),
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < MaxInventoryPosition; i++)
                    {
                        InventoryPositions[i] = i;
                    }
                }),
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < RemainingLivesCount_S1; i++)
                    {
                        RemainingLives[i] = i;
                    }
                })
            );

            Table1 = new QTable1(InventoryPositions, RemainingLives, OrderQuantities, ProductLife);
        }

        static int MakeLot(int product_count, int lot_size = LotSize)
        {
            product_count -= product_count % lot_size;
            product_count /= lot_size;
            return product_count;
        }

        public Task Save(Stream str)
        {
            return Table1.Save(str, Episodes + PreviousIterCount);
        }

        public async Task<double> Load(Stream str)
        {
            PreviousIterCount = await Table1.Load(str);
            var pre_epsilon_decay = Math.Exp(Math.Log(0.1) / PreviousIterCount);
            return Math.Pow(pre_epsilon_decay, MaxStepPerEpisode * PreviousIterCount);
        }
    }
}

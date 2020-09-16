using System;
using System.Collections.Generic;

namespace QLearningOnPerishableInventory
{
    public enum HMove
    {
        Left,
        Right,
    }

    public enum VMove
    {
        Top,
        Bottom,
    }

    public struct Position
    {
        public const int SIZE = 9;

        public int X;
        public int Y;

        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Position RandPos(Random r)
        {
            return new Position(r.Next(SIZE), r.Next(SIZE));
        }

        public void Move(int h = -1, int v = -1)
        {
            if (h == 0)
            {
                if(X > 0)
                    X--;
            }
            else if(h > 0)
            {
                if (X + 1 < SIZE)
                    X++;
            }

            if (v == 0) //top
            {
                if (Y > 0)
                    Y--;
            }
            else if(v > 0)
            {
                if(Y + 1 < SIZE)
                    Y++;
            }
        }

        public void MoveRandomly(Random r)
        {

        }

        public override string ToString()
        {
            return X.ToString() + "," + Y.ToString();
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Position position &&
                   X == position.X &&
                   Y == position.Y;
        }

        public static Position operator + (Position p1, Position p2)
        {
            return new Position(p1.X + p2.X, p1.Y + p2.Y);
        }
        public static Position operator - (Position p1, Position p2)
        {
            return new Position(p1.X - p2.X, p1.Y - p2.Y);
        }
        public static bool operator == (Position p1, Position p2)
        {
            return p1.X == p2.X && p1.Y == p2.Y;
        }
        public static bool operator != (Position p1, Position p2)
        {
            return p1.X != p2.X || p1.Y != p2.Y;
        }
    }

    public struct Move
    {
        public HMove H;
        public VMove V;

        public Move(HMove h, VMove v)
        {
            H = h;
            V = v;
        }
        
        public static Move GetRandomMove(Random r)
        {
            return new Move((HMove)r.Next(2), (VMove)r.Next(2));
        }

        public override string ToString()
        {
            return H.ToString() + "," + V.ToString();
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(H, V);
        }

        public override bool Equals(object obj)
        {
            return obj is Move move &&
                   H == move.H &&
                   V == move.V;
        }
    }

    public struct QTKey
    {
        public Position PositionF; 
        public Position PositionE; 
        public Move Action;

        public QTKey(Position position_F, Position position_E, Move action)
        {
            PositionF = position_F;
            PositionE = position_E;
            Action = action;
        }

        public override string ToString()
        {
            return PositionF.ToString() + "_" + PositionE.ToString() + " >>> " + Action.ToString();
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(PositionF, PositionE, Action);
        }

        public override bool Equals(object obj)
        {
            return obj is QTKey key &&
                   EqualityComparer<Position>.Default.Equals(PositionF, key.PositionF) &&
                   EqualityComparer<Position>.Default.Equals(PositionE, key.PositionE) &&
                   EqualityComparer<Move>.Default.Equals(Action, key.Action);
        }
    }


    public class SimpleQLearning
    {
        double LearningRate = 0.01;
        int Episodes = 50_000;
        int MaxStepPerEpisode = 200;
        double Epsilon = 1;
        double EpsilonDecay = 0.9998;
        double FutureDiscount = 0.98;

        const double FoodReward = 10;
        const double EnemyPenalty = -20;
        const double NothingPenalty = -1.0;

        public readonly double[] Rewards;

        public Dictionary<QTKey, double> QTable;

        public SimpleQLearning()
        {
            Rewards = new double[Episodes];
            QTable = new Dictionary<QTKey, double>(81 * 9);
        }


        private Move MoveOfMaxQValue(Position fpos, Position epos)
        {
            double max = -double.MaxValue;
            Move max_move = default;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    Move move = new Move((HMove)i, (VMove)j);
                    var tmp_key = new QTKey(fpos, epos, move);
                    var val = QTable[tmp_key];
                    if (val > max)
                    {
                        max = val;
                        max_move = move;
                    }
                }
            }

            return max_move;
        }


        public void Start(IProgress<double> pr)
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            Position Agent;
            Position Food;
            Position Enemy;

            for (int i = -Position.SIZE; i <= Position.SIZE; i++)
            {
                for (int j = -Position.SIZE; j <= Position.SIZE; j++)
                {
                    Position fpos = new Position(i, j);
                    for (int k = -Position.SIZE; k <= Position.SIZE; k++)
                    {
                        for (int l = -Position.SIZE; l <= Position.SIZE; l++)
                        {
                            Position epos = new Position(k, l);
                            QTable[new QTKey(fpos, epos, new Move(HMove.Left, VMove.Top))] = 0;
                            QTable[new QTKey(fpos, epos, new Move(HMove.Left, VMove.Bottom))] = 0;

                            QTable[new QTKey(fpos, epos, new Move(HMove.Right, VMove.Top))] = 0;
                            QTable[new QTKey(fpos, epos, new Move(HMove.Right, VMove.Bottom))] = 0;
                        }
                    }
                }
            }

            for (int i = 0; i < Episodes; i++)
            {
                Agent = Position.RandPos(rnd);
                Food = Position.RandPos(rnd);
                Enemy = Position.RandPos(rnd);

                double ep_reward = 0;

                if(Food == Agent || Food == Enemy || Enemy == Agent)
                {
                    i--;
                    continue;
                }

                for (int j = 0; j < MaxStepPerEpisode; j++)
                {
                    var relative_posf = Food - Agent;
                    var relative_pose = Enemy - Agent;
                    Move action;

                    if (rnd.NextDouble() < Epsilon) // explore
                    {
                        action = Move.GetRandomMove(rnd);
                    }
                    else // exploit
                    {
                        action = MoveOfMaxQValue(relative_posf, relative_pose);
                    }

                    var key = new QTKey(relative_posf, relative_pose, action);
                    Agent.Move((int)action.H, (int)action.V);

                    double reward;
                    if (Agent == Food)
                        reward = FoodReward;
                    else if (Agent == Enemy)
                        reward = EnemyPenalty;
                    else
                        reward = NothingPenalty;

                    ep_reward += reward;
                    
                    relative_posf = Food - Agent;
                    relative_pose = Enemy - Agent;

                    var max_q_action = MoveOfMaxQValue(relative_posf, relative_pose);
                    var max_future_q = QTable[new QTKey(relative_posf, relative_pose, max_q_action)];

                    QTable[key] = reward == FoodReward ? FoodReward : (1 - LearningRate) * QTable[key] + LearningRate * (reward + FutureDiscount * max_future_q);

                    if (reward == FoodReward || reward == EnemyPenalty)
                    {
                        break;
                    }

                }

                Rewards[i] = ep_reward;
                Epsilon *= EpsilonDecay;
                if (i % 100 == 0)
                    pr.Report(i * 100.0 / Episodes);
            }
        }
    }
}

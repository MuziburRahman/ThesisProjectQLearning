namespace ThesisProjectQLearning
{
    public struct QTableKey2
    {
        /// <summary>
        /// quantity
        /// </summary>
        public readonly int State;
        /// <summary>
        /// Order quantity
        /// </summary>
        public readonly int Action;

        public QTableKey2(int state, int action)
        {
            State = state;
            Action = action;
        }
    }

    public class QTable2 : QTableBase<QTableKey2>
    {
        public QTable2(int[] inv_pos, int[] oq) : base(inv_pos.Length * oq.Length)
        {

        }
    }
}

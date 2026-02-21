using System;
using System.ComponentModel;
using System.Collections.Generic;

namespace NHQTools.Extensions
{

    ////////////////////////////////////////////////////////////////////////////////////
    #region BindingListExtensions
    public static class BindingListExtensions
    {

        // Removes all elements from a BindingList<T> that match the conditions
        public static void RemoveAll<T>(this BindingList<T> list, Func<T, bool> condition)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var raiseEvents = list.RaiseListChangedEvents;

            try
            {
                list.RaiseListChangedEvents = false;

                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (condition(list[i]))
                        list.RemoveAt(i);
                }

            }
            finally
            {

                list.RaiseListChangedEvents = raiseEvents;

                if (raiseEvents)
                    list.ResetBindings();

            }

        }

    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region SortableBindingList
    // Sortable binding list for .NET 4.0
    // https://github.com/karenpayneoregon/datagridview-excel/blob/master/BindingListLibrary/SortableBindingList.cs
    public class SortableBindingList<T> : BindingList<T> where T : class
    {
        // Private
        private bool _isSorted;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private PropertyDescriptor _sortProperty;

        // Overrides
        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => _isSorted;
        protected override ListSortDirection SortDirectionCore => _sortDirection;
        protected override PropertyDescriptor SortPropertyCore => _sortProperty;

        ////////////////////////////////////////////////////////////////////////////////////
        #region Constructors
        public SortableBindingList() { }

        public SortableBindingList(IList<T> list) : base(list) { }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Sort Core
        protected override void RemoveSortCore()
        {
            _sortDirection = ListSortDirection.Ascending;
            _sortProperty = null;
            _isSorted = false;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            _sortProperty = prop;
            _sortDirection = direction;

            if (!(Items is List<T> list))
                return;

            list.Sort(Compare);

            _isSorted = true;
            //fire an event that the list has been changed.
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Compare
        private int Compare(T lhs, T rhs)
        {
            var result = OnComparison(lhs, rhs);

            //invert if descending
            if (_sortDirection == ListSortDirection.Descending)
                result = -result;

            return result;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        protected virtual int OnComparison(T lhs, T rhs)
        {
            var lhsValue = lhs == null ? null : _sortProperty.GetValue(lhs);
            var rhsValue = rhs == null ? null : _sortProperty.GetValue(rhs);

            if (lhsValue == null) // Both null: equal. Otherwise, null sorts before not null.
                return (rhsValue == null) ? 0 : -1;
         
            if (rhsValue == null)
                return 1; //first has value, second doesn't
     
            if (lhsValue is IComparable comparable)
                return comparable.CompareTo(rhsValue);
           
            if (lhsValue.Equals(rhsValue))
                return 0; //both are the same
           
            //not comparable, compare ToString
            return string.Compare(lhsValue.ToString(),
                rhsValue.ToString(), StringComparison.Ordinal);

        }

        #endregion

    }
    #endregion

}
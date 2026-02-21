using System;
using System.Windows.Forms;
using System.Collections.Generic;

// ReSharper disable MethodOverloadWithOptionalParameter

namespace NHQTools.Extensions
{
    ////////////////////////////////////////////////////////////////////////////////////
    #region DataGridViewExtensions
    public static class DataGridViewExtensions
    {

        ////////////////////////////////////////////////////////////////////////////////////
        #region Getters

        // Gets a value of a cell within a DataGridViewRow and converts it to the specified type
        public static T Get<T>(this DataGridViewRow row, string col) => (T)Convert.ChangeType(row.Cells[col].Value, typeof(T));

        // Gets a value of a cell within a DataGridViewRow and converts it to the specified type,
        // returning a default value if the cell is null
        public static T Get<T>(this DataGridViewRow row, string col, T defaultValue = default)
        {
            var value = row.Cells[col].Value;
            return value == null || value == DBNull.Value
                ? defaultValue
                : (T)Convert.ChangeType(value, typeof(T));
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Setters

        // Sets the value of the specified cell in the DataGridViewRow by column name.
        public static void Set(this DataGridViewRow row, string col, object value) => row.Cells[col].Value = value;

        #endregion      

    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region NonSelectingDataGridView
    public sealed class NonSelectingDataGridView : DataGridView
    {
        // Column names that act as action buttons
        public HashSet<string> ActionColumns { get; set; } = new HashSet<string>();

        // Column names that should not be highlighted when clicked, even if they are action columns
        public HashSet<string> NoHighlightColumns { get; set; } = new HashSet<string>();

        // Enable double buffering to reduce flicker during updates
        public NonSelectingDataGridView() => DoubleBuffered = true;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var hit = HitTest(e.X, e.Y);

            if (hit.Type == DataGridViewHitTestType.Cell)
            {
                var col = Columns[hit.ColumnIndex];

                if (ActionColumns.Contains(col.Name))
                {
                    ClearSelection();

                    // Select the clicked row for context, unless it's a non highlight column
                    Rows[hit.RowIndex].Selected = !NoHighlightColumns.Contains(col.Name);

                    OnCellMouseClick(new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex, e.X, e.Y, e));
                    OnCellClick(new DataGridViewCellEventArgs(hit.ColumnIndex, hit.RowIndex));

                    return;
                }
            }

            base.OnMouseDown(e);
        }

    }
    #endregion

}
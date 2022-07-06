using System.Text;

namespace EasyMinutesServer.Helpers
{
    public static class Html
    {
        public class Table : HtmlBase, IDisposable
        {
            public Table(StringBuilder sb, string classAttributes = "", string id = "") : base(sb)
            {
                var style = @"
<head>                    
    <style>
        table {
            border-collapse: collapse;
            border: 1px solid black;
            width: 100%;
            font-family: verdana;
        }
        thead {
            text-align: left;
            background-color: Navy;
            color: white;
            padding: 8px;
        }
        td {
            text-align: left;
            border: 1px solid black;
            padding: 8px;
        }

        tr:nth-child(even) {
            background-color: #D6EEEE;
        }
        #selected {
            background-color: tomato;
            color: white;
            padding: 40px;
            text-align: center;
        }
    </style>
</head>
";
                Append(style);
                Append("<table ");
                AddOptionalAttributes(classAttributes, id);
            }

            public void StartHead(string classAttributes = "", string id = "")
            {
                Append("<thead");
                AddOptionalAttributes(classAttributes, id);
            }

            public void EndHead()
            {
                Append("</thead>");
            }

            public void StartFoot(string classAttributes = "", string id = "")
            {
                Append("<tfoot");
                AddOptionalAttributes(classAttributes, id);
            }

            public void EndFoot()
            {
                Append("</tfoot>");
            }

            public void StartBody(string classAttributes = "", string id = "")
            {
                Append("<tbody");
                AddOptionalAttributes(classAttributes, id);
            }

            public void EndBody()
            {
                Append("</tbody>");
            }

            public void Dispose()
            {
                Append("</table>");
            }

            public Row AddRow(string classAttributes = "", string id = "")
            {
                return new Row(GetBuilder(), classAttributes, id);
            }
        }

        public class Row : HtmlBase, IDisposable
        {
            public Row(StringBuilder sb, string classAttributes = "", string id = "") : base(sb)
            {
                Append("<tr");
                AddOptionalAttributes(classAttributes, id);
            }
            public void Dispose()
            {
                Append("</tr>");
            }
            public void AddCell(string innerText, string classAttributes = "", string id = "", string colSpan = "")
            {
                Append("<td style='word -break:break-word' ");
                AddOptionalAttributes(classAttributes, id, colSpan);
                Append(innerText);
                Append("</td>");
            }
        }

        public abstract class HtmlBase
        {
            private StringBuilder _sb;

            protected HtmlBase(StringBuilder sb)
            {
                _sb = sb;
            }

            public StringBuilder GetBuilder()
            {
                return _sb;
            }

            protected void Append(string toAppend)
            {
                _sb.Append(toAppend);
            }

            protected void AddOptionalAttributes(string className = "", string id = "", string colSpan = "")
            {

                if (!id.IsNullOrEmpty())
                {
                    _sb.Append($" id=\"{id}\"");
                }
                if (!className.IsNullOrEmpty())
                {
                    _sb.Append($" class=\"{className}\"");
                }
                if (!colSpan.IsNullOrEmpty())
                {
                    _sb.Append($" colspan=\"{colSpan}\"");
                }
                _sb.Append(">");
            }
        }
    }
}

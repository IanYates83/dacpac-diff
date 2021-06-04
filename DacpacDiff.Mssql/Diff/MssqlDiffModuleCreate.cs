﻿using DacpacDiff.Core.Diff;
using DacpacDiff.Core.Model;
using DacpacDiff.Core.Output;
using System;
using System.Linq;

namespace DacpacDiff.Mssql.Diff
{
    public class MssqlDiffModuleCreate : BaseMssqlDiffBlock<DiffModuleCreate>
    {
        public bool UseStub { get; init; }
        public bool DoAsAlter { get; init; }

        public MssqlDiffModuleCreate(DiffModuleCreate diff)
            : base(diff)
        {
            UseStub = diff.Module.StubOnCreate;
            DoAsAlter = diff.DoAsAlter;
            _sql = null;
        }

        protected override void GetFormat(ISqlFileBuilder sb)
        {
            sb.Append(DoAsAlter ? "ALTER " : "CREATE ");

            switch (_diff.Module)
            {
                case FunctionModuleModel funcMod: // Stub
                    sb.AppendLine($"FUNCTION {funcMod.FullName} (");
                    if (funcMod.Parameters.Length > 0)
                    {
                        var argSql = funcMod.Parameters.Select(p => $"    {p.Name} {p.Type}"
                            + (p.HasDefault ? $" = {p.DefaultValue}" : "")
                            + (p.IsReadOnly ? " READONLY" : "")
                            + (p.IsOutput ? " OUTPUT" : "")).ToArray();
                        sb.AppendLine(string.Join(",\r\n", argSql));
                    }
                    sb.Append(") RETURNS ");

                    if (funcMod.ReturnTable != null)
                    {
                        var tblFields = funcMod.ReturnTable.Fields.Select(f => $"    [{f.Name}] {f.Type}"
                            + (!f.Nullable ? " NOT NULL" : ""));
                        sb.AppendLine($"{funcMod.ReturnType} TABLE (")
                            .AppendLine(string.Join(",\r\n", tblFields))
                            .Append(") AS ");
                    }
                    else if (funcMod.ReturnType == "TABLE")
                    {
                        sb.AppendLine("TABLE")
                            .AppendLine("AS");
                    }
                    else
                    {
                        sb.AppendLine(funcMod.ReturnType)
                            .AppendIf(() => "WITH RETURNS NULL ON NULL INPUT", funcMod.ReturnNullForNullInput).EnsureLine()
                            .Append("AS ");
                    }

                    if (UseStub)
                    {
                        appendStub(funcMod, sb);
                    }
                    else
                    {
                        sb.Append(funcMod.Body.Trim());
                    }
                    return;

                case IndexModuleModel idxMod:
                    sb.AppendIf(() => "UNIQUE ", idxMod.IsUnique)
                        .AppendIf(() => "CLUSTERED ", idxMod.IsClustered)
                        .Append($"INDEX [{idxMod.Name}] ON {idxMod.IndexedObject} ([")
                        .Append(string.Join("], [", idxMod.IndexedColumns))
                        .Append("])")
                        .AppendIf(() => " INCLUDE ([" + string.Join("], [", idxMod.IncludedColumns) + "])", idxMod.IncludedColumns.Length > 0)
                        .AppendIf(() => " WHERE " + idxMod.Condition, idxMod.Condition != null)
                        .AppendLine();
                    return;

                case ProcedureModuleModel procMod: // Stub
                    sb.Append($"PROCEDURE {procMod.FullName} AS ");

                    if (UseStub)
                    {
                        appendStub(procMod, sb);
                    }
                    else
                    {
                        sb.Append(procMod.Body.Trim());
                    }
                    return;

                case TriggerModuleModel trigMod:
                    sb.Append($"TRIGGER {trigMod.FullName} ON {trigMod.Parent} ")
                        .Append(trigMod.Before ? "AFTER " : "FOR ")
                        .AppendIf(() => "INSERT", trigMod.ForUpdate)
                        .AppendIf(() => ", ", trigMod.ForUpdate && (trigMod.ForInsert || trigMod.ForDelete))
                        .AppendIf(() => "UPDATE", trigMod.ForInsert)
                        .AppendIf(() => ", ", trigMod.ForInsert && trigMod.ForDelete)
                        .AppendIf(() => "DELETE", trigMod.ForDelete)
                        .Append("\r\nAS\r\n")
                        .Append(trigMod.Body)
                        .EnsureLine();
                    return;

                case ViewModuleModel viewMod: // Stub
                    // TODO: SCHEMABINDING
                    sb.Append($"VIEW {viewMod.FullName} AS ");

                    if (UseStub)
                    {
                        appendStub(viewMod, sb);
                    }
                    else
                    {
                        sb.Append(viewMod.Body.Trim());
                    }
                    return;
            }

            throw new NotImplementedException(_diff.Module.GetType().ToString());
        }

        private static void appendStub(FunctionModuleModel funcMod, ISqlFileBuilder sb)
        {
            if (funcMod.ReturnTable != null)
            {
                sb.AppendLine("BEGIN")
                    .AppendLine("    RETURN")
                    .AppendLine("END");
            }
            else if (funcMod.ReturnType == "TABLE")
            {
                sb.AppendLine("    RETURN SELECT 1 A");
            }
            else
            {
                sb.AppendLine("BEGIN")
                    .AppendLine("    RETURN NULL")
                    .AppendLine("END");
            }
        }

        private static void appendStub(ProcedureModuleModel procMod, ISqlFileBuilder sb)
        {
            sb.AppendLine("RETURN 0");
        }

        private static void appendStub(ViewModuleModel viewMod, ISqlFileBuilder sb)
        {
            sb.AppendLine("SELECT 1 A");
        }
    }
}

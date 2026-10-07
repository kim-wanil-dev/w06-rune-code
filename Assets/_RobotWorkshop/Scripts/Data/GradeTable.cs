using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GradeTable", menuName = "Robot Workshop/Grade Table")]
public sealed class GradeTable : ScriptableObject
{
    [Header("등급 및 능력 배율")]
    [SerializeField] private List<WorkshopGradeEntry> _grades = new List<WorkshopGradeEntry>();

    public IReadOnlyList<WorkshopGradeEntry> Grades => _grades;

    /// <summary>Finds a grade definition by its stable save identifier.</summary>
    public WorkshopGradeEntry Find(string gradeId)
    {
        return _grades.Find(grade => grade.Id == gradeId);
    }
}

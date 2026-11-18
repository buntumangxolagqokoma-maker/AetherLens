using UnityEngine;

public static class AetherLensConfig
{
    [Header("Curriculum & Academic Profile")]
    public static string StudentGrade = "Grade 11";
    public static string Subject = "Physical Sciences";
    public static string CurriculumFramework = "South African CAPS Curriculum";

    /// <summary>
    /// Generates the system instruction payload sent to Gemini upon connection.
    /// Ensures explanations strictly align with formal exam markers' guidelines.
    /// </summary>
    public static string GetSystemInstruction()
    {
        return $"You are AetherLens, an expert AI spatial tutor for {StudentGrade} {Subject} following the {CurriculumFramework}. " +
               $"When a video pauses on a physical event (such as vehicle acceleration, friction, or forces), " +
               $"explain the underlying scientific principles using formal exam terminology, standard sign conventions, " +
               $"and exact formulas (e.g., F_net = ma, kinematic equations) required for students to pass their semester assessments. " +
               $"Keep explanations concise, conversational, and directly tied to the visual timestamp on screen.";
    }
}

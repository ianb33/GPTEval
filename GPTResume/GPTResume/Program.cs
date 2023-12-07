using Microsoft.Extensions.Configuration;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using QuestPDF.Helpers;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using QuestPDF.Elements;
partial class Program
{
    static async Task Main(string[] args)
    {
        var resumeData = new ResumeData();

        //Example Resume Data

        // ResumeData resumeData = new ResumeData(
        // "Ian Berry",
        // "Student",
        // "484-867-7305",
        // "ianberry33@icloud.com",
        // "generate",
        // new[] { "Highschool Diploma, Easton Area High School, 2020-2024" },
        // new[] { "Lifeguard, Nazareth Borough Pool, 1 year", "Foodrunner, River Grille, 2 years", "Diving Coach, Blue Eagles Swim & Dive Team, 3 years" },
        // new[] { "Unity Developement", "Computer Programming" },
        // new[] { "CPR, AED, Lifeguarding and ECE" }
        // );

        QuestPDF.Settings.License = LicenseType.Community;
        // Configuration setup to manage API key
        var builder = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables();

        var configurationRoot = builder.Build();
        var key = configurationRoot.GetSection("OpenAIKey").Get<string>() ?? string.Empty;

        // Initialize OpenAI service
        var openAiService = new OpenAIService(new OpenAiOptions { ApiKey = key });

        // Collecting user inputs for the resume

        Console.WriteLine("Enter your name:");
        resumeData.Name = Console.ReadLine();
        if (resumeData.Name != null)
        {
            resumeData.Name = resumeData.Name;
        }
        Console.WriteLine("What is your current job/title?: ");
        resumeData.Title = Console.ReadLine();
        if (resumeData.Title != null)
        {
            resumeData.Title = resumeData.Title;
        }
        resumeData.Education.AddRange(CollectUserInput("Enter your education (degree, institution, graduation year), or type 'done' when finished:"));
        resumeData.WorkExperience.AddRange(CollectUserInput("Enter your work experience (job title, company, years), or type 'done' when finished:"));
        resumeData.Skills.AddRange(CollectUserInput("Enter your skills, or type 'done' when finished:"));
        resumeData.Credentials.AddRange(CollectUserInput("Enter your credentials, or type 'done' when finished: (eg. CPR, AED, etc.)"));

        Console.WriteLine("Enter your professional summary or type 'generate' to create one:");
        resumeData.ProfessionalSummary = Console.ReadLine();

        if (resumeData.ProfessionalSummary != null && resumeData.ProfessionalSummary.ToLower() == "generate")
        {
            // Generating professional summary using OpenAI
            var userPrompt = $"Create a brief professional summary that uses few pronouns for a resume including the following information with no placeholders (dont just use the information directly, make the writing unique): ";
            foreach (var item in resumeData.Education)
            {
                userPrompt += $"{item}, ";
            }
            foreach (var item in resumeData.WorkExperience)
            {
                userPrompt += $"{item}, ";
            }
            foreach (var item in resumeData.Skills)
            {
                userPrompt += $"{item}, ";
            }
            foreach (var item in resumeData.Credentials)
            {
                userPrompt += $"{item}, ";
            }
            var completionResult = openAiService.ChatCompletion.CreateCompletionAsStream(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    new(StaticValues.ChatMessageRoles.System, "You are a helpful assistant."),
                    new(StaticValues.ChatMessageRoles.User, userPrompt),
                },
                Model = Models.Gpt_3_5_Turbo_16k,
                MaxTokens = 200
            });

            resumeData.ProfessionalSummary = "";

            await foreach (var completion in completionResult)
            {
                if (completion.Successful)
                {
                    resumeData.ProfessionalSummary += completion.Choices.First().Message.Content;
                }
                else
                {
                    if (completion.Error == null)
                    {
                        throw new Exception("Unknown Error");
                    }

                    Console.WriteLine($"{completion.Error.Code}: {completion.Error.Message}");
                    return;
                }
            }
            resumeData.ProfessionalSummary = resumeData.ProfessionalSummary;
        }

        string filePath = "C:/Users/ianbe/Documents/MyResume.pdf";

        // Create and design the resume using QuestPDF
        var document = new ResumeDocument(resumeData);
        document.GeneratePdf(filePath);

        Console.WriteLine($"Resume generated successfully and saved to {filePath}");
    }
    static IEnumerable<string> CollectUserInput(string prompt)
    {
        List<string> inputs = new List<string>();
        Console.WriteLine(prompt);
        string? input;
        while ((input = Console.ReadLine()) != "done")
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                inputs.Add(input);
            }
            else
            {
                Console.WriteLine("Please enter valid information or type 'done'.");
            }
        }
        return inputs;
    }
}

public class ResumeData
{
    public string Name { get; set; }
    public string Title { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string ProfessionalSummary { get; set; } // Added for professional summary
    public List<string> Education { get; set; } = new List<string>();
    public List<string> WorkExperience { get; set; } = new List<string>();
    public List<string> Skills { get; set; } = new List<string>();
    public List<string> Credentials { get; set; } = new List<string>();

    public ResumeData()
    {
        Name = "";
        Title = "";
        Phone = "";
        Email = "";
        ProfessionalSummary = "";
    }

    public ResumeData(string name, string title, string phone, string email, string professionalSummary, string[] education, string[] workExperience, string[] skills, string[] credentials)
    {
        Name = name;
        Title = title;
        Phone = phone;
        Email = email;
        ProfessionalSummary = professionalSummary;
        Education = education.ToList();
        WorkExperience = workExperience.ToList();
        Skills = skills.ToList();
        Credentials = credentials.ToList();
    }
}

public class ResumeDocument : IDocument
{
    private ResumeData ResumeData { get; }

    public ResumeDocument(ResumeData resumeData)
    {
        ResumeData = resumeData;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(50);

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" | ");
                    x.TotalPages();
                });
            });
    }

    private void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text(ResumeData.Name, TextStyle.Default.Size(20).Bold());
            column.Item().Text(ResumeData.Title, TextStyle.Default.Size(15).SemiBold());
        });
    }

    private void ComposeContent(QuestPDF.Infrastructure.IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(20);

            // Professional Summary section
            column.Item().Column(stack =>
            {
                stack.Item().Text("Professional Summary", TextStyle.Default.Size(14).Bold().Underline());
                stack.Item().Text(ResumeData.ProfessionalSummary);
            });

            // Education section

            column.Item().Column(column =>
            {
                column.Item().Text("Education", TextStyle.Default.Size(14).Bold().Underline());
                foreach (var item in ResumeData.Education)
                {
                    column.Item().Text(item);
                }
            });


            // Work Experience section

            column.Item().Column(column =>
            {
                column.Item().Text("Work Experience", TextStyle.Default.FontSize(14).Bold().Underline());
                foreach (var item in ResumeData.WorkExperience)
                {
                    column.Item().Text(item);
                }
            });


            // Skills section
            column.Item().Column(column =>
            {
                column.Item().Text("Skills", TextStyle.Default.FontSize(14).Bold().Underline());
                foreach (var item in ResumeData.Skills)
                {
                    column.Item().Text(item);
                }
            });


            // Credentials section

            column.Item().Column(column =>
            {
                column.Item().Text("Credentials", TextStyle.Default.FontSize(14).Bold().Underline());
                foreach (var item in ResumeData.Credentials)
                {
                    column.Item().Text(item);
                }
            });
        });
    }
}





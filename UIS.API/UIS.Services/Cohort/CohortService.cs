﻿using CsvHelper.Configuration;
using CsvHelper;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UIS.DAL.Constants;
using UIS.DAL.DTO;
using Microsoft.AspNetCore.Http;

using AutoMapper;
using UIS.DATA;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace UIS.Services.Cohort
{
    public class CohortService : ICohortService
    {
        private readonly IStudentsRepository _studentsRepository;
        private readonly IMapper _mapper;

        public CohortService(IStudentsRepository studentsRepository, IMapper mapper)
        {
            _studentsRepository = studentsRepository;
            _mapper = mapper;
        }

        public async Task<List<CohortUpdateDataDTO>> ExtractMoodleSyncDataAsync(HttpClient client, string jwt, IFormFile csvFile)
        {
            var allMoodleCohorts = await GetMoodleCohortsAsync(client, jwt);
            var studentsFromCSVGroupedByCohorts = ExtractStudentDataByCohortsFromCSV(csvFile);

            List<CohortUpdateDataDTO> cohortsUpdateData = new List<CohortUpdateDataDTO>();

            // Iterates through every cohort in moodle
            foreach (var moodleCohort in allMoodleCohorts)
            {
                // vzima userIds
                // iterirame vseki user i vzimame info za nego i go mahame dolu polse vuv foreacha
                // refactor da chekva za null

                // Takes the student ids from the given moodle cohort
                var studentIdsFromMoodle = await GetStudentsIDsFromMoodleCohortsAsync(client, moodleCohort.id, jwt);

                // If there are no students from the CSV file mathing the current cohort, skip the cohort iteration
                bool studentsFromCSVContainsCohort = studentsFromCSVGroupedByCohorts.ContainsKey(moodleCohort.name);

                // Gets the students from the CSV matching the same cohort
                var studentsFromCsv = new List<StudentInfoDTO>();
                if (studentsFromCSVContainsCohort)
                {
                    studentsFromCsv=studentsFromCSVGroupedByCohorts[moodleCohort.name]; 
                }

                // List of student ids from moodle, matching the given cohort
                var studentsIdsFromMoodle = studentIdsFromMoodle[0].userids ?? throw new Exception();

                var moodleUpdateData = await GetMoodleUpdateDataAsync(client, jwt, studentsIdsFromMoodle, studentsFromCsv, moodleCohort.name);

                if (moodleUpdateData is not null)
                {
                    moodleUpdateData.CohortId = moodleCohort.id;

                    cohortsUpdateData.Add(moodleUpdateData);
                }
            }

            return cohortsUpdateData;
        }

        public async Task<List<MoodleCohortsDTO>> GetMoodleCohortsAsync(HttpClient client, string jwt)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_cohort_get_cohorts"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json")
            });

            var response = await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            List<MoodleCohortsDTO> allMoodleCohorts = new List<MoodleCohortsDTO>();

            if (response != null && response.IsSuccessStatusCode)
            {
                var getMoodleCohortsResponseContent = await response.Content.ReadAsStringAsync();
                allMoodleCohorts = JsonSerializer.Deserialize<List<MoodleCohortsDTO>>(getMoodleCohortsResponseContent);
            }

            return allMoodleCohorts;
        }

        public async Task<List<MoodleCohortUsersDTO>> GetStudentsIDsFromMoodleCohortsAsync(HttpClient client, int cohortId, string jwt)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_cohort_get_cohort_members"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("cohortids[]", cohortId.ToString())
            });

            var responseMessage = await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            var response = await responseMessage.Content.ReadAsStringAsync();
            var studentsFromMoodle = JsonSerializer.Deserialize<List<MoodleCohortUsersDTO>>(response);

            return studentsFromMoodle;
        }
        public async Task AddStudentToMoodleCohortAsync(HttpClient client, string jwt, List<StudentInfoDTO> studentsToAddToCohort, string cohortId)
        {
            foreach (var student in studentsToAddToCohort)
            {
                bool userExists = await CheckIfUserExistsByUsernameAsync(client, student.Username, jwt);

                if (userExists == false)
                {
                    await CreateMoodleUserAsync(client, student, jwt);
                }

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_cohort_add_cohort_members"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("members[0][cohorttype][type]", "id"),
                    new KeyValuePair<string, string>("members[0][cohorttype][value]", cohortId),
                    new KeyValuePair<string, string>("members[0][usertype][type]", "username"),
                    new KeyValuePair<string, string>("members[0][usertype][value]", student.Username),
                });

                await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            }
        }
        public async Task DeleteStudentsFromMoodleCohortAsync(HttpClient client, string jwt, List<StudentInfoDTO> studentsRemovedFromCohort, string cohortId)
        {
            foreach (var student in studentsRemovedFromCohort)
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_cohort_delete_cohort_members"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("members[0][cohortid]", cohortId),
                    new KeyValuePair<string, string>("members[0][userid]", student.Id.ToString()),
                });

                await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            }
        }
        public List<StudentInfoDTO> ExtractStudentDataFromCSV(IFormFile csvFile)
        {
            List<StudentInfoDTO> records = new List<StudentInfoDTO>();

            // Read the CSV file and map it to a list of StudentInfoDTO objects
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null, // Ignore missing headers
                HasHeaderRecord = true, // The first row is the header
                Encoding = Encoding.UTF8, // Set the encoding
            };

            using (var reader = new StreamReader(csvFile.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                records = csv.GetRecords<StudentInfoDTO>().ToList();
            }

            return records;
        }

        public async Task<StudentInfoDTO?> GetUserByIdAsync(HttpClient client, int userId, string jwt)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_user_get_users_by_field"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("field", "id"),
                    new KeyValuePair<string, string>("values[0]", userId.ToString())
            });

            var responseMessage = await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            if (responseMessage.IsSuccessStatusCode)
            {
                var response = await responseMessage.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<List<StudentInfoDTO>>(response);

                return userInfo[0];
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> CheckIfUserExistsByUsernameAsync(HttpClient client, string username, string jwt)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_user_get_users_by_field"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("field", "username"),
                    new KeyValuePair<string, string>("values[0]", username)
            });

            var responseMessage = await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);
            var response = await responseMessage.Content.ReadAsStringAsync();

            // TODO: Find a better approach
            if (response == "[]")
            {
                return false;
            }

            return true;
        }

        public async Task CreateMoodleUserAsync(HttpClient client, StudentInfoDTO studentInfo, string jwt)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("wstoken", jwt),
                    new KeyValuePair<string, string>("wsfunction", "core_user_create_users"),
                    new KeyValuePair<string, string>("moodlewsrestformat", "json"),
                    new KeyValuePair<string, string>("users[0][username]", studentInfo.Username),
                    new KeyValuePair<string, string>("users[0][password]", "St_" + studentInfo.Username),
                    new KeyValuePair<string, string>("users[0][firstname]", studentInfo.FirstName),
                    new KeyValuePair<string, string>("users[0][lastname]", studentInfo.LastName),
                    new KeyValuePair<string, string>("users[0][email]", studentInfo.Email),
            });

            var response = await client.PostAsync(MoodleAuthConstants.RestAPIUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception();
            }
        }

        public async Task SaveStudentsInfoAsync(List<StudentInfoDTO> students)
        {
            foreach (var student in students)
            {
                StudentInfo studentInfo = _mapper.Map<StudentInfo>(student);

                try
                {
                    // Check if the student is already added
                    var existingStudent = await _studentsRepository.GetStudentByUsernameAsync(student.Username);

                    if (existingStudent == null)
                    {
                        await _studentsRepository.AddAsync(studentInfo);
                    }
                }
                catch (Exception ex)
                {
                    // Handle the exception or log it
                }
            }

            await _studentsRepository.SaveChangesAsync();
        }

        private async Task<CohortUpdateDataDTO> GetMoodleUpdateDataAsync(HttpClient client, string jwt, List<int> studentsIdsFromMoodle, List<StudentInfoDTO> studentsFromCsv, string cohortName)
        {
            // If there is no cohort in moodle, add it?
            // Keeps a copy of the id of the students from moodle and removes a student from the list if he is present in the moodle CSV
            // If the list is not empty, the users inside are to be removed
            var trackStudentsToRemoveFromMoodle = studentsIdsFromMoodle.ToList();
            var allStudentsIdsAfterRemove = studentsIdsFromMoodle.ToList();

            // Removes the students that are already in moodle from the lists of students from moodle and csv
            // If the student is in moodle but not in the csv -> delete student from the cohort
            foreach (var moodleStudentId in studentsIdsFromMoodle)
            {
                var moodleStudent = await GetUserByIdAsync(client, moodleStudentId, jwt);

                var isStudentAlreadyInMoodle = studentsFromCsv.FirstOrDefault(x => x.Username == moodleStudent.Username || x.Email == moodleStudent.Email);
                if (isStudentAlreadyInMoodle != null)
                {
                    // If the student from CSV is already in Moodle, remove the student from the current list of moodle and csv students
                    studentsFromCsv.Remove(isStudentAlreadyInMoodle);
                    trackStudentsToRemoveFromMoodle.Remove(moodleStudentId);
                }
            }

            List<StudentInfoDTO> studentsToRemoveFromCohort = new List<StudentInfoDTO>();

            // Gets the students that will be deleted from cohort
            foreach (var moodleStudentId in trackStudentsToRemoveFromMoodle)
            {
                var student = await GetUserByIdAsync(client, moodleStudentId, jwt);
                studentsToRemoveFromCohort.Add(student);
                allStudentsIdsAfterRemove.Remove(moodleStudentId);
            }

            List<StudentInfoDTO> allStudentsAfterRemove = new List<StudentInfoDTO>();

            foreach (var studentId in allStudentsIdsAfterRemove)
            {
                var student = await GetUserByIdAsync(client, studentId, jwt);
                allStudentsAfterRemove.Add(student);
            }

            if (studentsToRemoveFromCohort.Count == 0 && studentsFromCsv.Count == 0)
            {
                return null;
            }

            CohortUpdateDataDTO dataToUpdateMoodleDTO = new CohortUpdateDataDTO();
            dataToUpdateMoodleDTO.StudentsToRemoveFromCohort = studentsToRemoveFromCohort;
            dataToUpdateMoodleDTO.StudentsToAddToCohort = studentsFromCsv;
            dataToUpdateMoodleDTO.AllStudents = allStudentsAfterRemove;
            dataToUpdateMoodleDTO.CohortName = cohortName;

            // Returns the list of students for upload and remove
            return dataToUpdateMoodleDTO;
        }

        private Dictionary<string, List<StudentInfoDTO>> ExtractStudentDataByCohortsFromCSV(IFormFile csvFile)
        {
            var unsortedRecords = ExtractStudentDataFromCSV(csvFile);

            // Groups the students by cohortId and sets the dictionary key to the cohortId
            var groupedRecords = unsortedRecords.GroupBy(s => s.Cohort1).ToDictionary(g => g.Key, g => g.ToList());

            return groupedRecords;
        }

        public async Task<List<DiscordStudentInfoDTO>> GetAllStudentsFromMoodleAsync(HttpClient client, string jwt)
        {
            //pulls all moodle cohorts
            var allMoodleCohorts = await GetMoodleCohortsAsync(client, jwt);

            List<DiscordStudentInfoDTO> allStudentData = new List<DiscordStudentInfoDTO>();

            foreach (var cohort in allMoodleCohorts)
            {
                var studentMoodleIds = (await GetStudentsIDsFromMoodleCohortsAsync(client, cohort.id, jwt))[0].userids;
                if (studentMoodleIds == null || studentMoodleIds.Count == 0) continue;

                var students = new List<DiscordStudentInfoDTO>();

                var cohortNameElements = cohort.name.Split('/', StringSplitOptions.TrimEntries);
                //skips cohort if the cohort format is incorrect
                if (cohortNameElements.Count() < 3) continue;
                //extracts data needed for the discord bot from the cohort name
                //assumes the following cohort naming convention: <faculty> / <specialty> / <year of degree enrollment>[ - <master's>]
                var faculty = cohortNameElements[0];
                var major = cohortNameElements[1];
                var degree="";
                //gets year of enrollment and degree
                var cohortYear = cohortNameElements[2].Split('-', StringSplitOptions.TrimEntries);
                int year;
                //skips cohort if the cohort format is incorrect
                if (!int.TryParse(cohortYear[0], out year))
                    continue;
                //assumes a 4-year bachelor's and 2-year master's; ideally moodle courses would hold data for yof education for obtaining it
                int maxYears = 4;
                if (cohortYear.Count() == 1)
                    degree = "Бакалавър";
                else
                {
                    degree = "Магистър";
                    maxYears = 2;
                }
                //iterates through all moodle students by their id
                foreach (var studentId in studentMoodleIds)
                {
                    //fetches moodle user and skips the current iteration if not found
                    var studentFromMoodle = await GetUserByIdAsync(client, studentId, jwt);
                    if (studentFromMoodle == null) continue;
                    //builds discord data from moodle data
                    var student = new DiscordStudentInfoDTO();
                    
                    student.names = studentFromMoodle.FirstName + " " + studentFromMoodle.LastName;
                    student.facultyNumber = studentFromMoodle.Username;
                    student.specialty = major;
                    student.faculty = faculty;
                    student.degree = degree;
                    student.course = Math.Min(((int)((DateTime.Today - new DateTime(year, 9, 1)).TotalDays))/365+1, maxYears);
                    
                    students.Add(student);
                }
                allStudentData.AddRange(students);
            }
            return allStudentData;
        }
    }
}

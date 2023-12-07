export interface StudentInfo {
    id: string;
    idNumber: string;
    auth: string;
    firstName: string;
    lastName: string;
    email: string;
}

export interface MoodleCohort {
    cohortid: number;
    userids: number[];
}

export interface DataToUpdateCohort {
    cohortId: string;
    studentsToRemoveFromCohort: StudentInfo[];
    studentsToAddToCohort: StudentInfo[];
}
const mysql = require('mysql2');
// Async function to execute the query with parameters
// Function to execute the query with parameters using Promise
function createConnection() {
  const connection = mysql.createConnection({
    host: 'localhost',
    port: 4300,
    user: 'root',
    password: 'admin',
    database: 'uis_student'
  });
  return connection;
}

function executeQuery(facultyNumber, guildId) {
  return new Promise((resolve, reject) => {
    // Create a connection to the database
    const connection = createConnection();

    // SQL query
    const query = `
      SELECT DiscordId 
      FROM discorddata d 
      INNER JOIN students s ON d.StudentId = s.Id 
      WHERE s.Username = ? AND d.GuildId = ?;
    `;

    // Connect to the database
    connection.connect(error => {
      if (error) {
        reject('An error occurred while connecting to the DB: ' + error);
        return;
      }
      console.log('Connected to the database.');

      // Execute the query with the provided parameters
      connection.query(query, [facultyNumber, guildId], (error, results) => {
        if (error) {
          reject('An error occurred while executing the query: ' + error);
        } else {
          resolve(results);
        }
        connection.end();
      });
    });
  });
}
function fetchAuthorizedStudent(discordId, guildId)
{
  return new Promise((resolve, reject)=>{
    const connection=createConnection();
    connection.connect();
    //get faculty num of student if he's authorized on the server
    connection.query(
      'SELECT Username FROM discorddata INNER JOIN students ON discorddata.StudentId = students.Id WHERE DiscordId = ? AND GuildId = ?',
      [discordId, guildId],
      (error, result) => {
        connection.end();

        if (error) reject(error);
        else resolve(result);
      }
    )
  })
}

async function facultyDB(faculty) {
  const connection = createConnection();

  // Return a new Promise
  return new Promise((resolve, reject) => {
    connection.connect();

    connection.query(
      'SELECT Major FROM guilddata WHERE guildName = ?',
      [faculty],
      (error, results) => {
        connection.end(); // Make sure to end the connection after the query

        if (error) {
          reject(error); // Reject the Promise if there is an error
        } else {
          resolve(results); // Resolve the Promise with the results
        }
      }
    );
  });
}

async function discrordDBCheck(facultyNumber){
  const connection = createConnection();

  return new Promise((resolve, reject) =>{
    connection.connect();

    connection.query(
      'SELECT Id FROM students WHERE Username = ?',
      [facultyNumber],
      (error, results) =>{
        connection.end();
  
        if(error){
          reject(error);
        }else{
          resolve(results);
        }
      }
  
    )

  })

}

function checkStudentAuth(discordId, guildId){
  const connection = createConnection();

  return new Promise((resolve, reject) =>{
    connection.connect();
    const sql = 'Select StudentId from discorddata where DiscordId = ? and GuildId = ?';
    connection.query(sql, [discordId, guildId], (error, results) => {
      if (error) {
        reject('An error occurred while executing the query: ' + error);
      } else {
        resolve(results);
      }
      connection.end();
    });   
      }
    )
  }


// Function to insert data into discorddata table
function insertIntoDiscordData(discordData) {
  const connection = createConnection();

  const sql = 'INSERT INTO discorddata SET ?';
  connection.query(sql, discordData, (error, results) => {
    if (error) {
      console.error('Error occurred:', error);
      return;
    }
    console.log('Insert successful:', results);
  });
}

module.exports = {insertIntoDiscordData,checkStudentAuth,discrordDBCheck,facultyDB,executeQuery,fetchAuthorizedStudent};


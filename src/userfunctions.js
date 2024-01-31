const {executeQuery, fetchAuthorizedStudent, facultyDB} = require('./database.js');
const {delay, roleNumber, bachelorRoles} = require('./utilites.js');

async function syncUsers(interaction,client,bachelorRoles,masterRoles){
  process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  fetch('https://localhost:7059/api/User/discord-sync')
  .then(response => {
      if (!response.ok) {
          throw new Error('Network response was not ok');
      }
      return response.json();
  })
  .then(data => {
      getGuildById(interaction.guild.id,client).then(myguild=>{
        myguild.members.fetch().then(members=>{
          //iterates through every member to synchronize their data with up-to-date data from moodle cohorts
          for (const member of members)
          {
            //gets student's data from db if authorized
            fetchAuthorizedStudent(member[0],myguild.id).then(result=>{
              if (result!="") 
              {
                facultyDB(myguild.name).then(res=>{
                  //gets instances of this student in all his cohorts
                  const student = data.filter(element=>element.facultyNumber==result[0].Username&&element.specialty==res[0].Major)
                  //clears student's roles/server name if he doesn't currently belong to any cohort 
                  if (student.length===0)
                  {
                    clearRolesAndUsername(member[1], myguild);
                  }
                  else {
                    //initializes array to hold student's cohorts iterated through
                    let studentInfoInCheckedCohorts=Array(0);
                    student.forEach(element=>{
                    //checks if this is the highest course the user's enrolled in
                    const isHighestCourse=(studentInfoInCheckedCohorts.length===0 || studentInfoInCheckedCohorts.find(e=>{
                      if (e.degree === element.degree) return e.course>element.course;
                      if (e.degree === "Бакалавър") return false;
                      return true;
                      })==undefined)
                    //only changes username if this is the highest course
                    if (isHighestCourse)
                    {
                      const username = `${element.names} (${element.course}. курс)`;
                      //console.log(JSON.stringify(result))
                      setName(member[0],username,myguild);
                    }
                    let role;
                    if(element.degree === "Бакалавър"){
                      role = bachelorRoles[element.course];
                    getCourseRole(role,myguild).then(role =>{
                      setRole(role,member[0],myguild);
                    }) 

                    }else if(element.degree === "Магистър"){
                      //masters keep their roles from their bachelors'
                      role = masterRoles[element.course];
                      getCourseRole(role,myguild).then(role =>{
                        setRole(role,member[0],myguild);
                        
                      })
                    }
                    //removes roles for higher courses if this is the highest course the (authorized) student's enrolled in
                    if (isHighestCourse) removeRoles(member[0],role,myguild);

                    studentInfoInCheckedCohorts.push(element);
                  })
                }})
              }
              else {
                //removes all of the user's roles if not authorized
                clearRolesAndUsername(member[1], myguild);
              }
            }).then(() => delay(1000));
          }
        })
      })
    // Process your data here
      // for(let i = 0;i<data.length;i++ ) {
      //   executeQuery(data[i].facultyNumber,interaction.guild.id).then(result=>{
      //     if(result != ""){
      //       const names = data[i].names.split(" ");
      //       const username = `${names[0]} ${names[names.length-1]} (${data[i].course}. курс)`;
      //       const degree = data[i].oks;
      //       console.log(JSON.stringify(result))
      //       for(let j=0;j<result.length;j++){
      //         getGuildById(interaction.guild.id,client).then(myguild=>{
      //           setName(result[j].DiscordId,username,myguild);
      //         if(degree === "Бакалавър"){
      //           const role = bachelorRoles[data[i].course];
      //          getCourseRole(role,myguild).then(role =>{
      //           setRole(role,result[j].DiscordId,myguild);
      //         }) 

      //          removeRoles(result[j].DiscordId,role,myguild)

      //         }else if(degree === "Магистър"){

      //           const role = masterRoles[data[i].course];
      //           getCourseRole(role,myguild).then(role =>{
      //             setRole(role,result[j].DiscordId,myguild);
                  
      //         })
      //         //removeBachelorRoles(result[j].DiscordId,myguild);
              
      //       }
      //         })
      //       }
      //     }
      //   })
      //   .then(() => delay(1000));
      // }
      
  })
  .catch(error => {
      console.error('There has been a problem with your fetch operation:', error);
  });
return "Success";
}

function clearRolesAndUsername(guildMember, guild)
{
  //skips bots and owner => assumes the bot has maximum possible rights in the server
  if (guildMember.user.bot||guild.ownerId==guildMember.user.id) {console.log("neposlushen\n\nneposlushen"); return;}
  //clears all roles sans @everyone/Administrator and server name
  let roles=guildMember.roles.cache.filter(role => role.name != "@everyone" && role.name != "Administrator");
  guildMember.roles.remove(roles).then(()=>{
    guildMember.setNickname(null).then(console.log(`${guildMember.user.tag} has dropped out`));
  });
}

function setRole(role,discordID,guild){
  // Fetch the member asynchronously
  guild.members.fetch(discordID)
  .then(member => {
    // Check if the member is found
    if (member) {
      // Add the role to the member
      member.roles.add(role)
        .then(() => {
          console.log(`Added role ${role.name} to user ${member.user.tag}`);
        })
        .catch(error => {
          console.error(`Error adding role: ${error}`);
        });
    } else {
      console.error(`Member not found with ID: ${discordID}`);
    }
  })
  .catch(error => {
    console.error(`Error fetching member: ${error}`);
  });
  
  }
  
  function setName(discordID,username,guild){
    guild.members.fetch(discordID)
    .then(member => {
      // Check if the member is found
      if (member) {
       
        // Change the username (nickname) of the member
        member.setNickname(username)
          .then(() => {
            console.log(`Changed username to ${username} for user ${member.user.tag}`);
          })
          .catch(error => {
            console.error(`Error changing username: ${error}`);
          });
        }
      });
  
  }
  

  async function getGuildById(guildID,client) {
    try {
        const guild = await client.guilds.fetch(guildID);
        return guild;
    } catch (error) {
        console.error(`Error fetching guild: ${error.message}`);
        return null;
    }
  }
  
  async function getCourseRole(courseName,myguild) {
    try {
      let role = await myguild.roles.cache.find((n=> n.name === courseName));
      return role;
    }catch(error){
      console.error(`Error getting role: ${error.message}`);
    }
  }
  
  async function getUser(discordID,myguild){
    const user = await myguild.members.fetch(discordID);
  
    return user;
  }
  
  async function removeRoles(discordID,role,myguild){
    const roleAsNumber = roleNumber[role];
    myguild.members.fetch(discordID)
    .then(member=>{  
      const allRoles = member.roles.cache;
      allRoles.forEach(element => {
        const currentRole = roleNumber[element.name];
        if (currentRole==undefined) return;
        if(roleAsNumber < currentRole){
          member.roles.remove(element)
          console.log(`Role ${element.name} removed for ${member.user.tag}`);
        }
      });
    })
  }
  
  function checkAuth(discordID, myguild) {
    return new Promise((resolve, reject) => {
        myguild.members.fetch(discordID)
            .then(member => {
                const allRoles = member.roles.cache;
                let isAuthorized = true;
                allRoles.forEach(element => {
                    console.log(`Role name is ${element.name}`);
                    if (element.name == "authorized") {
                        isAuthorized = false;
                    }
                });
                resolve(isAuthorized);
            })
            .catch(error => {
                reject(error); // Handle errors, such as the member not being found
            });
    });
  }
  
  async function removeBachelorRoles(discordID,myguild){
    myguild.members.fetch(discordID)
    .then(member=>{
      const allRoles = member.roles.cache;
  
      allRoles.forEach(element=>{
        for (const [key, value] of Object.entries(bachelorRoles)) {
          if (element.name === value){
            member.roles.remove(element);
            console.log(`Removed bachelor role ${element.name}`);
          }
        }
      })
  
    })
  }

  module.exports = {removeBachelorRoles,checkAuth,removeRoles,getUser,getCourseRole,getGuildById,setName,setRole,syncUsers};
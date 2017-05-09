PowerBI api project
----------

Main Feature. There are 5 btn in homepage
 1. Get list dataset
 2. Add new dataset
 3. Push data to table of dataset
 4. Popular data
 5. Clear data
 
Flow (when click one of btn):
 1. Check access token. If exist go to (2), else go to (4)
 2. Check access token expired or not. If not expired go to (3), else go to (6)
 3. Make http request to PoweBI API enpoint, show result to screen
 4. Redirect user to AD login page. After login success, go to (5)
 5. Save access token & refresh token to database. Then go to (3)
 6. Request new access token from PowerBI, using refresh token. If okay go to (5), else go to (4)
 
Prepare:
 1. TenantId -> this is ID of your AD
 2. ClientID, ClientSecret -> follow this: https://powerbi.microsoft.com/en-us/documentation/powerbi-developer-register-a-client-app/
 3. RedirectUrl -> for test, this is http://localhost:your_port
 4. ConnectionString -> database to store access & refresh token
 5. Change UseSql in web.config = 0 to use memory local instead sql server

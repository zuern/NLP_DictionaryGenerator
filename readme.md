# Dictionary Generator
This project's function is to generate dictionary files for the [Natural Language Analysis Tool](https://github.com/Propheis/Natural-Language-Analysis-Tool). It accomplishes this by reading a word list from a text file and looking up definitions for those words from the [Merriam-Webster Dictionary](http://www.m-w.com/). 
##Accessing the API
Once you have downloaded the project you have to set your API key in order to use the program.
In order to access the API, you need to provide an API key for the Merriam-Webster dictionary. You can get one of those [here](http://dictionaryapi.com/).

Now that you have the API key, open the `Keys.cs` file and paste your key between the double-quotes as shown below.
`public static string APIKey = "YourAPIKeyHere";`
# Sir.DbUtil

Sir.DbUtil is a console application for writing and validating local Resin databases.

## Write
`
cd [path_of_resin_repo]

dbutil write [path_of_local_json_file] [path_of_resin_data_directory] [name_of_collection] [num_of_docs_to_skip] [num_of_docs_to_take] [segment_size]
`

E.g. 

`dbutil write C:\\wikidata\\svwiki-20190624-cirrussearch-content.json\\svwiki-20190624-cirrussearch-content.json c:\\data\\resin www_se 0 10000 1000`

## Validate

`
cd [path_of_resin_repo]

dbutil validate [path_of_resin_data_directory] [name_of_collection] [num_of_docs_to_skip] [num_of_docs_to_take]
`

E.g. 

`dbutil validate c:\\data\\resin www_se 0 10000`

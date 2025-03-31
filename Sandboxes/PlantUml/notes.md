echo "@startuml\nAlice -> Bob: Hello\nBob --> Alice: Hi\n@enduml" > diagram.txt && java -jar plantuml.jar diagram.txt

printf "@startuml\nAlice -> Bob: Hello\nBob --> Alice: Hi\n@enduml\n" > diagram2.txt
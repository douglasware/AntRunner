
import json

def get_books_by_category(file_path, category):
    """
    Retrieve books based on the given category.

    Parameters:
    file_path (str): The path to the JSON file containing the books data.
    category (str): The category to filter books by.

    Returns:
    list: A list of books that match the given category.
    """
    with open(file_path, 'r') as file:
        books = json.load(file)
        filtered_books = [book for book in books if book['type'].lower() == category.lower()]
        
    return filtered_books

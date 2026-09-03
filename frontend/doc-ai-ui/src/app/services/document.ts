import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Document } from '../models/document.model';
import { AskResponse } from '../models/ask-response.model';
import { SearchResult } from '../models/search-result.model';
import { environment } from '../../environments/environment';


@Injectable({
  providedIn: 'root'
})
export class DocumentService {

  private apiUrl = `${environment.apiUrl}/api/Documents`;

  constructor(private http: HttpClient) { }

  getDocuments(): Observable<Document[]> {
    return this.http.get<Document[]>(this.apiUrl);
  }

  uploadDocument(file: File): Observable<Document> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<Document>(`${this.apiUrl}/upload`, formData);
  }

  downloadDocument(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download`, {
      responseType: 'blob'
    });
  }

  semanticSearch(query: string): Observable<SearchResult[]> {
    return this.http.post<SearchResult[]>(
      `${environment.apiUrl}/api/search/semantic-search`,
      { query }
    );
  }

  askQuestion(query: string): Observable<AskResponse> {
    return this.http.post<AskResponse>(
      `${environment.apiUrl}/api/rag/ask`,
      { query }
    );
  }
}
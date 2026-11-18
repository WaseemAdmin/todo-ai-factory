import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Todo, TodoUpdate, TodoWrite } from './todo.model';

@Injectable({ providedIn: 'root' })
export class TodoApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/todos`;

  list(isCompleted?: boolean): Promise<Todo[]> {
    const params = isCompleted === undefined ? new HttpParams() : new HttpParams().set('isCompleted', isCompleted);
    return firstValueFrom(this.http.get<Todo[]>(this.url, { params }));
  }

  get(id: string): Promise<Todo> {
    return firstValueFrom(this.http.get<Todo>(`${this.url}/${id}`));
  }

  create(request: TodoWrite): Promise<Todo> {
    return firstValueFrom(this.http.post<Todo>(this.url, request));
  }

  update(id: string, request: TodoUpdate): Promise<Todo> {
    return firstValueFrom(this.http.put<Todo>(`${this.url}/${id}`, request));
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.url}/${id}`));
  }
}

export function apiErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const details = error.error as { title?: string; errors?: Record<string, string[]> } | null;
    const fieldMessages = details?.errors ? Object.values(details.errors).flat().join(' ') : '';
    return fieldMessages || details?.title || `Request failed (${error.status || 'network error'}).`;
  }
  return 'Something went wrong. Please try again.';
}
